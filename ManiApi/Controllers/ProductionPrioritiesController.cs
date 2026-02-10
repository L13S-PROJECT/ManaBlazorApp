using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/production-priorities")]
public class ProductionPrioritiesController : ControllerBase
        {
            private readonly AppDbContext _db;

            public ProductionPrioritiesController(AppDbContext db)
            {
                _db = db;
            }

[HttpGet]
public async Task<IActionResult> Get()
    {
                        var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            var list = new List<object>();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
            SELECT
                bp.ID            AS BatchProductId,
                bp.Version_Id    AS VersionId,
                p.Product_Code   AS ProductCode,
                p.Product_Name   AS ProductName,
                c.Category_Name  AS CategoryName,
                v.Version_Name   AS VersionName,
                bp.Planned_Qty   AS Planned,
                bp.is_priority   AS IsPriority,

                -- Detailed Y = cik detaļu šim BatchProduct (no taskiem)
                (
                    SELECT COUNT(DISTINCT ts.ProductToPart_ID)
                    FROM tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND ts.Step_Type = 1
                ) AS DetailedY,


            -- Detailed X = cik detaļu ir procesā (šim BatchProduct)
            (
                SELECT COUNT(DISTINCT ts.ProductToPart_ID)
                FROM tasks t
                JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                WHERE t.BatchProduct_ID = bp.ID
                AND t.IsActive = 1
                AND ts.Step_Type = 1
                AND t.Tasks_Status <> 5
            ) AS DetailedX,

(
    CASE
        WHEN (
            SELECT COUNT(DISTINCT ts.ProductToPart_ID)
            FROM tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            WHERE t.BatchProduct_ID = bp.ID
              AND t.IsActive = 1
              AND ts.Step_Type = 1
              AND t.Tasks_Status <> 5
        ) > 0
        THEN 1
        ELSE 0
    END
) AS DetailedHasStarted,


    -- Detailed IS DONE = visi Detailed taski šim BatchProduct ir 3
(
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            WHERE t.BatchProduct_ID = bp.ID
              AND t.IsActive = 1
              AND ts.Step_Type = 1
        )
        AND NOT EXISTS (
            SELECT 1
            FROM tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            WHERE t.BatchProduct_ID = bp.ID
              AND t.IsActive = 1
              AND ts.Step_Type = 1
              AND t.Tasks_Status <> 3
        )
        THEN 1 ELSE 0
    END
) AS DetailedIsDone,

                -- Detailed IN PROGRESS
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status IN (2,3)
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status <> 3
                    )
                    THEN bp.Planned_Qty
                    ELSE 0
                END AS DetailedInProgress,

                -- Detailed FINISH
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status = 3
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status IN (1,2,5)
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 2
                        AND t.Tasks_Status IN (2,3)
                    )
                    THEN bp.Planned_Qty
                    ELSE 0
                END AS DetailedFinish,

                -- Assembly IN PROGRESS
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 2
                        AND t.Tasks_Status IN (2,3)
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 2
                        AND t.Tasks_Status IN (1,2,5)
                    )
                    THEN bp.Planned_Qty
                    ELSE 0
                END AS Assembly,

                -- Done (Assembly stock)
                (
                    SELECT COALESCE(SUM(sm.Stock_Qty), 0)
                    FROM stock_movements sm
                    WHERE sm.IsActive = 1
                    AND sm.Move_Type = 'ASSEMBLY'
                    AND sm.BatchProduct_ID = bp.ID
                ) AS Done,

                -- Finishin X = cik detaļu ir procesā (šim BatchProduct)
                (
                    SELECT COALESCE(SUM(t.Qty_Done), 0)
                    FROM tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND ts.Step_Type = 3
                    AND t.Tasks_Status IN (1,2,3)
                ) AS FinishingX,

                -- Finishing Y = cik detaļu ir DONE (šim BatchProduct)

                (
                    SELECT COALESCE(SUM(sm.Stock_Qty), 0)
                    FROM stock_movements sm
                    WHERE sm.IsActive = 1
                    AND sm.BatchProduct_ID = bp.ID
                    AND sm.Move_Type = 'ASSEMBLY'
                ) AS FinishingY,

                -- Finishing FINISHED = pabeigtie (status = 3)
                (
                    SELECT COALESCE(SUM(t.Qty_Done), 0)
                    FROM tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND ts.Step_Type = 3
                    AND t.Tasks_Status = 3
                ) AS FinishingDone,


                -- Finishing IN PROGRESS
                (
                    SELECT COALESCE(SUM(t.Qty_Done),0)
                    FROM tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND ts.Step_Type = 3
                    AND t.Tasks_Status = 2
                ) AS FinishingInProgress

            FROM batches_products bp
            JOIN versions v   ON v.ID = bp.Version_Id
            JOIN products p   ON p.ID = v.Product_ID
            LEFT JOIN categories c ON c.ID = p.Category_ID
            WHERE
                bp.IsActive = 1
                AND EXISTS (
                    SELECT 1
                    FROM tasks t
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND t.Tasks_Status <> 3
                );";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new
                {
                    BatchProductId      = reader.GetInt32(0),
                    VersionId           = reader.GetInt32(1),
                    ProductCode         = reader.GetString(2),
                    ProductName         = reader.GetString(3),
                    CategoryName        = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    VersionName         = reader.GetString(5),
                    Planned             = reader.GetInt32(6),
                    IsPriority          = reader.GetBoolean(7),

                   DetailedY = reader.GetInt32(8),
                    DetailedX = reader.GetInt32(9),
                    DetailedHasStarted = reader.GetBoolean(10),
                    DetailedIsDone     = reader.GetBoolean(11),

                DetailedInProgress  = reader.GetInt32(12),
                DetailedFinish      = reader.GetInt32(13),
                Assembly            = reader.GetInt32(14),
                Done                = reader.GetInt32(15),
                FinishingX          = reader.GetInt32(16),
                FinishingY          = reader.GetInt32(17),
                FinishingDone       = reader.GetInt32(18),
                FinishingInProgress = reader.GetInt32(19)

                });

                }
            }

            return Ok(list);
    }

            [HttpPut("{batchProductId}")]
        public async Task<IActionResult> Put(int batchProductId, [FromBody] bool isPriority)
        {
            var bp = await _db.BatchProducts
                .FirstOrDefaultAsync(x => x.ID == batchProductId);

            if (bp == null)
                return NotFound();

            bp.is_priority = isPriority;

            await _db.SaveChangesAsync();

            return NoContent();
        }

[HttpGet("list")]
public async Task<IActionResult> GetList()
{
    var rows = await _db.BatchProducts
        .AsNoTracking()
        .Where(bp =>
            bp.IsActive &&
            _db.Tasks.Any(t =>
                t.BatchProduct_ID == bp.ID &&
                t.IsActive &&
                t.Tasks_Status != 3
            )
        )
        .Select(bp => new
        {
            BatchProductId = bp.ID,

            Planned = bp.Planned_Qty,
            Done = bp.Done_Qty,
            IsPriority = bp.is_priority,

            VersionId = bp.Version_Id,

            VersionName = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Select(v => v.VersionName)
                .FirstOrDefault(),

            ProductName = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Join(_db.Products,
                      v => v.ProductId,
                      p => p.Id,
                      (v, p) => p.ProductName)
                .FirstOrDefault(),

            ProductCode = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Join(_db.Products,
                      v => v.ProductId,
                      p => p.Id,
                      (v, p) => p.ProductCode)
                .FirstOrDefault(),

            CategoryName = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Join(_db.Products,
                      v => v.ProductId,
                      p => p.Id,
                      (v, p) => p.CategoryId)
                .Join(_db.Categories,
                      pid => pid,
                      c => c.Id,
                      (pid, c) => c.CategoryName)
                .FirstOrDefault()
        })
        .ToListAsync();

    return Ok(rows);
}

// GET: api/production-priorities/impact
[HttpGet("impact")]
public async Task<IActionResult> GetPriorityImpact()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    var result = new List<object>();

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT
    wc.WorkCentr_Name AS WorkCenter,
    COUNT(t.ID)       AS TaskCount
FROM workcentr_type wc
LEFT JOIN toppartsteps s
    ON s.WorkCentr_ID = wc.ID
    AND s.IsActive = 1
LEFT JOIN tasks t
    ON t.TopPartStep_ID = s.ID
    AND t.IsActive = 1
    AND t.Tasks_Status IN (1,2)
LEFT JOIN batches_products bp
    ON bp.ID = t.BatchProduct_ID
    AND bp.IsActive = 1
    AND bp.is_priority = 1
WHERE wc.IsActive = 1
  AND bp.ID IS NOT NULL
GROUP BY wc.WorkCentr_Name
ORDER BY wc.WorkCentr_Name;
";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
                {
                    WorkCenter = reader.GetString(0),
                    TaskCount  = reader.GetInt32(1)
                });


        }
    }

    return Ok(result);
}


}





