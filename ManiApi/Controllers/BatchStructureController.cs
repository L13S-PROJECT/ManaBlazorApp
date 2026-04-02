using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using System.Text.Json.Serialization;

[ApiController]
[Route("api/[controller]")]
public class PartsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PartsController(AppDbContext db)
    {
        _db = db;
    }

    // GET: api/parts/details-by-version?versionId=1
    [HttpGet("details-by-version")]
    public async Task<IActionResult> GetDetailPartsByVersion([FromQuery] int versionId)
    {
        var rows = await _db.ProductTopParts.AsNoTracking()
            .Where(pt => pt.VersionId == versionId && pt.IsActive)
            .Join(
                _db.TopParts.Where(tp => tp.IsActive && tp.Stage == 1), // 👈 tikai DETAIL
                pt => pt.TopPartId,
                tp => tp.Id,
                (pt, tp) => new PartRowDto
                    {
                        TopPartId = pt.TopPartId,
                        TopPartName = tp.TopPartName,
                        TopPartCode = tp.TopPartCode,
                        Quantity = pt.QtyPerProduct,
                        ProductToPartId = pt.Id
                    })
            .ToListAsync();

        return Ok(rows);
    }

    [HttpGet("summary")]
public async Task<IActionResult> GetPartsSummary([FromQuery] int versionId)
{
    var result = await (
        from ptp in _db.ProductTopParts
        join tp in _db.TopParts
            on ptp.TopPartId equals tp.Id
        where ptp.IsActive
            && ptp.VersionId == versionId
            && tp.IsActive
            && tp.Stage == 1

        select new
        {
            TopPartId = tp.Id,
            TopPartName = tp.TopPartName,

            OkQty = 0,
            ReservedQty = 0,
            FreeQty = 0
        }
    ).ToListAsync();

    return Ok(result);
}

[HttpGet("product-summary")]
public async Task<IActionResult> GetProductSummary()
{
    var result = await (
        from p in _db.Products
        where p.IsActive

        join v in _db.ProductVersions
            on p.Id equals v.ProductId
        where v.IsActive

        join ptp in _db.ProductTopParts
            on v.Id equals ptp.VersionId
        where ptp.IsActive

        join tp in _db.TopParts
            on ptp.TopPartId equals tp.Id
        where tp.IsActive && tp.Stage == 1   // tikai DETAIL

        group new { p, v, ptp, tp } by new
            {
                ProductId = p.Id,
                p.ProductName,
                VersionId = v.Id,
                v.VersionName
            }
        into g

        select new
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                VersionId = g.Key.VersionId,
                VersionName = g.Key.VersionName,

                TotalQty = g.Count(),

                Parts = g.Select(x => x.tp.TopPartName).Distinct().ToList()
            }

    ).ToListAsync();

    return Ok(result);
}

[HttpGet("in-production")]
public async Task<IActionResult> GetPartsInProduction()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    ptp.Version_ID AS VersionId,
    ptp.TopPart_ID AS TopPartId,
    tp.TopPart_Name AS TopPartName,

   SUM(
    CASE 
       WHEN EXISTS (
            SELECT 1
            FROM tasks t2
            JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
            JOIN producttopparts ptp2 ON ptp2.ID = ts2.ProductToPart_ID
            WHERE t2.IsActive = 1
                AND ts2.Step_Type = 1
                AND ptp2.ID = ptp.ID
                AND t2.BatchProduct_ID = bp.ID
                AND t2.Tasks_Status IN (1,2)
        )
        THEN COALESCE(bp.Planned_Qty,0) * ptp.Qty_Per_product
        ELSE 0
    END
) AS InProduction

FROM producttopparts ptp

JOIN toppart tp
    ON tp.ID = ptp.TopPart_ID
    AND tp.IsActive = 1
    AND tp.Stage = 1

LEFT JOIN batches_products bp
    ON (
        (bp.Version_Id = ptp.Version_ID AND bp.ProductToPart_ID IS NULL)
        OR
        (bp.ProductToPart_ID = ptp.ID)
    )
    AND bp.IsActive = 1

LEFT JOIN batches b
    ON b.ID = bp.Batch_Id
    AND b.IsActive = 1
    AND b.Batches_Statuss = 1

WHERE ptp.IsActive = 1
AND b.Batches_Statuss = 1

GROUP BY ptp.Version_ID, ptp.TopPart_ID, tp.TopPart_Name
ORDER BY ptp.Version_ID, tp.TopPart_Name;
";

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
       list.Add(new
            {
                VersionId = r.GetInt32(0),
                TopPartId = r.GetInt32(1),
                TopPartName = r.GetString(2),
                InProduction = r.IsDBNull(3) ? 0 : r.GetInt32(3)
            });
    }

    return Ok(list);
}

[HttpGet("ok-by-version")]
public async Task<IActionResult> GetOkByVersion()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    ptp.Version_ID AS VersionId,
    ptp.TopPart_ID AS TopPartId,

    SUM(
        CASE 
            WHEN t.Tasks_Status = 3 
                THEN COALESCE(bp.Planned_Qty,0) * ptp.Qty_Per_product
            ELSE 0
        END
        ) AS OkQty

FROM tasks t

JOIN toppartsteps ts 
    ON ts.ID = t.TopPartStep_ID
    AND ts.Step_Type = 1   -- tikai DETAIL

JOIN producttopparts ptp 
    ON ptp.ID = ts.ProductToPart_ID
    AND ptp.IsActive = 1

JOIN toppart tp
    ON tp.ID = ptp.TopPart_ID
    AND tp.IsActive = 1
    AND tp.Stage = 1

JOIN batches_products bp
    ON bp.ID = t.BatchProduct_ID
    AND bp.IsActive = 1

WHERE t.IsActive = 1

AND NOT EXISTS (
    SELECT 1
    FROM tasks t2
    JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
    WHERE t2.BatchProduct_ID = t.BatchProduct_ID
      AND ts2.ProductToPart_ID = ts.ProductToPart_ID
      AND ts2.Step_Order > ts.Step_Order
      AND t2.IsActive = 1
)

AND NOT EXISTS (
    SELECT 1
    FROM tasks t2
    JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
    WHERE t2.BatchProduct_ID = t.BatchProduct_ID
      AND ts2.Step_Type = 2
      AND t2.Tasks_Status IN (2,3)
      AND t2.IsActive = 1
)

GROUP BY ptp.Version_ID, ptp.TopPart_ID
ORDER BY ptp.Version_ID, ptp.TopPart_ID;
";

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
            {
                VersionId = r.GetInt32(0),
                TopPartId = r.GetInt32(1),
                OkQty = r.IsDBNull(2) ? 0 : r.GetInt32(2)
            });
    }

    return Ok(list);
}

[HttpGet("reserved-by-version")]
public async Task<IActionResult> GetReservedByVersion()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    bp.Version_Id AS VersionId,
    ptp.TopPart_ID AS TopPartId,

SUM(
    CASE  
        WHEN t.Tasks_Status IN (1,5)
        THEN COALESCE(bp.Planned_Qty,0) * ptp.Qty_Per_product
            ELSE 0
        END
    ) AS ReservedQty

FROM tasks t

JOIN batches_products bp
    ON bp.ID = t.BatchProduct_ID
    AND bp.IsActive = 1

JOIN toppartsteps ts
    ON ts.ID = t.TopPartStep_ID
    AND ts.Step_Type = 2   -- tikai Assembly

JOIN producttopparts ptp
    ON ptp.Version_ID = bp.Version_Id
    AND ptp.IsActive = 1

JOIN toppart tp
    ON tp.ID = ptp.TopPart_ID
    AND tp.Stage = 1
    AND tp.IsActive = 1

WHERE t.IsActive = 1

AND NOT EXISTS (
    SELECT 1
    FROM tasks t2
    JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
    WHERE t2.BatchProduct_ID = t.BatchProduct_ID
      AND ts2.Step_Type = 2
      AND t2.Tasks_Status IN (2,3)
      AND t2.IsActive = 1
)

GROUP BY bp.Version_Id, ptp.TopPart_ID
ORDER BY bp.Version_Id, ptp.TopPart_ID;
";

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            VersionId = r.GetInt32(0),
            TopPartId = r.GetInt32(1),
            ReservedQty = r.IsDBNull(2) ? 0 : r.GetInt32(2)
        });
    }

    return Ok(list);
}

[HttpGet("stock-by-version")]
public async Task<IActionResult> GetStockByVersion()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    sm.Version_ID AS VersionId,
    ptp.TopPart_ID AS TopPartId,
    SUM(sm.Stock_Qty) AS StockQty

FROM stock_movements sm

JOIN tasks t
    ON t.ID = sm.Task_ID

JOIN toppartsteps ts
    ON ts.ID = t.TopPartStep_ID

JOIN producttopparts ptp
    ON ptp.ID = ts.ProductToPart_ID

JOIN toppart tp
    ON tp.ID = ptp.TopPart_ID
    AND tp.Stage = 1
    AND tp.IsActive = 1

WHERE sm.IsActive = 1

GROUP BY sm.Version_ID, ptp.TopPart_ID;
";

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            VersionId = r.GetInt32(0),
            TopPartId = r.GetInt32(1),
            StockQty = r.IsDBNull(2) ? 0 : r.GetInt32(2)
        });
    }

    return Ok(list);
}

public class PartRowDto
{
    [JsonPropertyName("TopPartId")]
    public int TopPartId { get; set; }

    [JsonPropertyName("TopPartName")]
    public string? TopPartName { get; set; }

    [JsonPropertyName("TopPartCode")]
    public string? TopPartCode { get; set; }

    [JsonPropertyName("Quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("productToPartId")]
    public int ProductToPartId { get; set; }
}

}