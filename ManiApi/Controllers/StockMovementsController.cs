using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;
using MySqlConnector;
using System;



namespace ManiApi.Controllers
{
public sealed class MoveRequest
{
    public int Version_ID { get; set; }
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public int Qty { get; set; }
    public int? Task_ID { get; set; }
    public DateTime? Created_At { get; set; }

    // Var sūtīt vai nu gatavu BatchProduct_ID
    public int? BatchProduct_ID { get; set; }

    // Vai tikai Batch_Id (batches.ID) – tad API pats atrod BatchProduct_ID
    public int? Batch_Id { get; set; }
}


    [ApiController]
    [Route("api/[controller]")]
    public class StockMovementsController : ControllerBase
    {
        private readonly AppDbContext _db;
        

        public StockMovementsController(AppDbContext db)
        {
            _db = db;
        }

// POST: api/stockmovements/move
       
      [HttpPost("move")]
public async Task<IActionResult> Move([FromBody] MoveRequest dto)
{
    if (dto.Qty <= 0) return BadRequest("Qty must be positive.");
    if (dto.From == dto.To) return BadRequest("From and To cannot be the same.");

    var now = dto.Created_At ?? DateTime.UtcNow;

    // 1) Mēģinām paņemt BatchProduct_ID – vai nu no DTO, vai atrodam pēc Batch_Id + Version_ID
    int? batchProductId = dto.BatchProduct_ID;

    if ((batchProductId is null || batchProductId <= 0) &&
        dto.Batch_Id is int batchId && batchId > 0)
    {
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ID
            FROM batches_products
            WHERE Batch_Id  = @batch
              AND Version_Id = @ver
              AND IsActive   = 1
            LIMIT 1;";

        var pBatch = cmd.CreateParameter();
        pBatch.ParameterName = "@batch";
        pBatch.Value = batchId;
        cmd.Parameters.Add(pBatch);

        var pVer = cmd.CreateParameter();
        pVer.ParameterName = "@ver";
        pVer.Value = dto.Version_ID;
        cmd.Parameters.Add(pVer);

        var obj = await cmd.ExecuteScalarAsync();
        if (obj != null && obj != DBNull.Value)
            batchProductId = Convert.ToInt32(obj);
    }

    if (batchProductId is null or <= 0)
        return BadRequest("Either BatchProduct_ID or valid Batch_Id + Version_ID is required.");

    int bpId = batchProductId.Value;

    var fromType = Enum.Parse<MoveType>(dto.From, ignoreCase: true);
    var toType   = Enum.Parse<MoveType>(dto.To,   ignoreCase: true);

    var fromRow = new StockMovement
{
    Version_ID      = dto.Version_ID,
    BatchProduct_ID = bpId,
    Move_Type       = fromType,
    Stock_Qty       = -dto.Qty,
    Created_At      = now,
    Task_ID         = dto.Task_ID,
    IsActive        = true
};


    var toRow = new StockMovement
    {
        Version_ID      = dto.Version_ID,
        BatchProduct_ID = bpId,
        Move_Type       = toType,
        Stock_Qty       = dto.Qty,
        Created_At      = now,
        Task_ID         = dto.Task_ID,
        IsActive        = true
    };

    _db.StockMovements.Add(fromRow);
    _db.StockMovements.Add(toRow);
    await _db.SaveChangesAsync();

    return Ok(new { FromId = fromRow.Id, ToId = toRow.Id });
}

[HttpGet("summary")]
public async Task<IActionResult> Summary([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1) Planned (no batches_products)
    int planned = 0;
    await using (var cmdPl = conn.CreateCommand())
    {
        cmdPl.CommandText = @"
SELECT COALESCE(SUM(bp.Planned_Qty), 0)
FROM batches_products bp
JOIN batches b ON b.ID = bp.Batch_Id
WHERE bp.Version_Id = @vid
  AND bp.IsActive = 1
  AND b.IsActive = 1
  AND b.Batches_Statuss = 1;";
        cmdPl.Parameters.Add(new MySqlParameter("@vid", versionId));
        planned = Convert.ToInt32(await cmdPl.ExecuteScalarAsync());
    }

    // 2) Visi pārvietojumi no stock_movements (tas ir “kur atrodas apjoms”)
    int detailed = 0, assembly = 0, finishing = 0, stock = 0, scrap = 0, outQty = 0;

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT sm.Move_Type, COALESCE(SUM(sm.Stock_Qty), 0) AS Qty
FROM stock_movements sm
WHERE sm.IsActive = 1
  AND sm.Version_ID = @vid
GROUP BY sm.Move_Type;";
        cmd.Parameters.Add(new MySqlParameter("@vid", versionId));

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var type = r.GetString(0);
            var qty  = r.GetInt32(1);

            switch (type)
            {
                case "DETAILED":  detailed  = qty; break;
                case "ASSEMBLY":  assembly  = qty; break;
                case "FINISHING": finishing = qty; break;
                case "STOCK":     stock     = qty; break;
                case "SCRAP":     scrap     = qty; break;
                case "OUT":       outQty    = qty; break;
            }
        }
    }

    return Ok(new
    {
        Planned = planned,
        Detailed = detailed,
        DetailedFinish = 0,      // ja tev vajag vēlāk - var pielikt, bet Planningam nav kritiski
        Assembly = assembly,
        AssemblyFinish = 0,      // idem
        Finishing = finishing,
        Stock = stock,
        Scrap = scrap,
        Out = outQty
    });
}

        // GET: api/stockmovements/finishing-totals?batchProductId=123
[HttpGet("finishing-totals")]
public async Task<IActionResult> GetFinishingTotals([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var total = await _db.StockMovements
        .Where(x => x.BatchProduct_ID == batchProductId &&
                    x.IsActive &&
                    x.Move_Type == MoveType.FINISHING)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    return Ok(new { TotalFinishing = total });
}

// GET: api/stockmovements/finishing-list
[HttpGet("finishing-list")]
public async Task<IActionResult> GetFinishingList([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var list = await _db.StockMovements
        .Where(x => x.BatchProduct_ID == batchProductId &&
                    x.IsActive &&
                    (x.Move_Type == MoveType.FINISHING || x.Move_Type == MoveType.ASSEMBLY))
        .OrderBy(x => x.Created_At)
        .Select(x => new
        {
            x.Id,
            x.Move_Type,
            x.Stock_Qty,
            x.Created_At,
            x.Task_ID
        })
        .ToListAsync();

    return Ok(list);
}

// GET: api/stockmovements/finishing-total
[HttpGet("finishing-total")]
public async Task<IActionResult> GetFinishingTotal([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var total = await _db.StockMovements
        .Where(x => x.BatchProduct_ID == batchProductId &&
                    x.IsActive &&
                    x.Move_Type == MoveType.FINISHING)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    return Ok(total);
}

// GET: api/stockmovements/finishing-by-batch
[HttpGet("finishing-by-batch")]
public async Task<IActionResult> GetFinishingByBatch([FromQuery] int batchId)
{
    if (batchId <= 0)
        return BadRequest("batchId is required.");

    // 1) Nolasām visus BatchProduct_ID šai partijai ar SQL — jo Tev nav DbSet
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    List<int> batchProductIds = new();

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT ID
            FROM batches_products
            WHERE Batch_Id = @bid
              AND IsActive = 1;";

        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = batchId;
        cmd.Parameters.Add(p);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            batchProductIds.Add(reader.GetInt32(0));
    }

    if (batchProductIds.Count == 0)
        return Ok(new List<object>());   // nav datu

    // 2) Nolasām FINISHING kustības priekš šiem ID
    var result = await _db.StockMovements
        .Where(x =>
    x.IsActive &&
    x.Move_Type == MoveType.FINISHING &&
    x.BatchProduct_ID != null &&
    batchProductIds.Contains(x.BatchProduct_ID.Value)
)

        .GroupBy(x => x.BatchProduct_ID)
        .Select(g => new
        {
            BatchProduct_ID = g.Key,
            TotalFinishing = g.Sum(x => x.Stock_Qty)
        })
        .ToListAsync();

    return Ok(result);
}

  // GET: api/stockmovements/finishing-summary-by-batch
[HttpGet("finishing-summary-by-batch")]
public async Task<IActionResult> GetFinishingSummaryByBatch([FromQuery] int batchId)
{
    if (batchId <= 0)
        return BadRequest("batchId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1) Iegūstam batchProductId + Planned Qty + VersionId
    List<(int BatchProductId, int PlannedQty)> parts = new();

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT ID, Planned_Qty
            FROM batches_products
            WHERE Batch_Id = @bid
              AND IsActive = 1;";

        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = batchId;
        cmd.Parameters.Add(p);

        using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var bpId = rd.GetInt32(0);
            var planned = rd.GetInt32(1);
            parts.Add((bpId, planned));
        }
    }

    if (parts.Count == 0)
        return Ok(new List<object>());

    var batchProductIds = parts.Select(p => p.BatchProductId).ToList();

    // 2) Kopā nosūtīts uz Finishing
  var finishingTotals = await _db.StockMovements
    .Where(x =>
        x.IsActive &&
        x.Move_Type == MoveType.FINISHING &&
        x.BatchProduct_ID != null &&
        batchProductIds.Contains(x.BatchProduct_ID.Value)
    )
    .GroupBy(x => x.BatchProduct_ID)
    .Select(g => new
    {
        BatchProduct_ID = g.Key ?? 0,
        SentToFinishing = g.Sum(x => x.Stock_Qty)
    })
    .ToDictionaryAsync(x => x.BatchProduct_ID, x => x.SentToFinishing);


    // 3) Apvienojam vienā atbildē UI pusē ērtā formā
    var result = parts.Select(p =>
    {
        finishingTotals.TryGetValue(p.BatchProductId, out var sent);

        return new
        {
            p.BatchProductId,
            Planned = p.PlannedQty,
            SentToFinishing = sent,
            Remaining = Math.Max(p.PlannedQty - sent, 0)
        };
    });

    return Ok(result);
}

// GET: api/stockmovements/totals-by-version?versionId=123
[HttpGet("totals-by-version")]
public async Task<IActionResult> GetTotalsByVersion([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    // paņemam VISAS kustības šai versijai
    var grouped = await _db.StockMovements
        .Where(x => x.IsActive && x.Version_ID == versionId)
        .GroupBy(x => x.Move_Type)
        .Select(g => new
        {
            MoveType = g.Key,
            Qty = g.Sum(m => m.Stock_Qty)
        })
        .ToListAsync();

    // noklusētās nulles, ja kāda tipa nav vispār
    int detailed = 0;
    int assembly = 0;
    int finishing = 0;
    int stock = 0;
    int scrap = 0;
    int @out = 0;

    foreach (var row in grouped)
    {
        switch (row.MoveType)
        {
            case MoveType.DETAILED:
                detailed = row.Qty;
                break;
            case MoveType.ASSEMBLY:
                assembly = row.Qty;
                break;
            case MoveType.FINISHING:
                finishing = row.Qty;
                break;
            case MoveType.STOCK:
                stock = row.Qty;
                break;
            case MoveType.SCRAP:
                scrap = row.Qty;
                break;
            case MoveType.OUT:
                @out = row.Qty;
                break;
        }
    }

    // atdodam vienkāršu, skaidru objektu
    var result = new
    {
        VersionId = versionId,
        Detailed  = detailed,
        Assembly  = assembly,
        Finishing = finishing,
        Stock     = stock,
        Scrap     = scrap,
        Out       = @out
    };

    return Ok(result);
}


// GET: /api/stockmovements/stock-by-version-active?versionId=123
[HttpGet("stock-by-version-active")]
public async Task<IActionResult> GetStockByVersionActive([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COALESCE(SUM(sm.Stock_Qty), 0) AS StockQty
FROM stock_movements sm
JOIN batches_products bp ON bp.ID = sm.BatchProduct_ID AND bp.IsActive = 1
JOIN batches b ON b.ID = bp.Batch_Id AND b.IsActive = 1
WHERE sm.IsActive = 1
  AND sm.Version_ID = @vid
  AND sm.Move_Type = 'STOCK'
  AND b.Batches_Statuss = 1;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@vid";
    p.Value = versionId;
    cmd.Parameters.Add(p);

    var val = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    return Ok(new { stock = val });
}

// GET: api/stockmovements/available-by-batch?versionId=3
[HttpGet("available-by-batch")]
public async Task<IActionResult> GetAvailableByBatch([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    bp.ID              AS BatchProductId,
    b.Batches_Code     AS BatchCode,
    sm.Move_Type       AS MoveType,
    SUM(sm.Stock_Qty) AS AvailableQty

FROM stock_movements sm
JOIN batches_products bp
    ON bp.ID = sm.BatchProduct_ID
JOIN batches b
    ON b.ID = bp.Batch_Id
WHERE sm.IsActive = 1
  AND sm.Version_ID = @vid
  AND sm.Move_Type IN ('STOCK', 'ASSEMBLY')
GROUP BY
    bp.ID,
    b.Batches_Code,
    sm.Move_Type;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@vid";
    p.Value = versionId;
    cmd.Parameters.Add(p);

    var rows = new List<object>();

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        rows.Add(new
        {
            BatchProductId = reader.GetInt32(0),
            BatchCode      = reader.GetString(1),
            MoveType       = reader.GetString(2),
            AvailableQty   = reader.GetInt32(3)
        });
    }

    return Ok(rows);
}

[HttpGet("assembly-total")]
public async Task<IActionResult> GetAssemblyTotal([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    // 1) Reālais ASSEMBLY stock
    var assemblyStock = await _db.StockMovements
        .Where(x =>
            x.IsActive &&
            x.BatchProduct_ID == batchProductId &&
            x.Move_Type == MoveType.ASSEMBLY)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    // 2) Jau rezervētais FINISHING apjoms (status 1 un 2)
var reservedForFinishing = await _db.Tasks
    .Join(_db.TopPartSteps,
          t => t.TopPartStep_ID,
          ts => ts.Id,
          (t, ts) => new { t, ts })
    .Where(x =>
        x.t.IsActive &&
        x.t.BatchProduct_ID == batchProductId &&
        x.ts.StepType == 3 &&          // FINISHING
        x.t.Tasks_Status == 1 &&       // TIKAI rezervētie
        x.t.Qty_Done > 0)
    .SumAsync(x => (int?)x.t.Qty_Done) ?? 0;
    // Pieejamais ASSEMBLY apjoms FINISHING uzsākšanai
    var available = Math.Max(assemblyStock - reservedForFinishing, 0);

    return Ok(available);
}

// GET: api/stockmovements/sold-before-finishing?batchProductId=123
[HttpGet("sold-before-finishing")]
public async Task<IActionResult> GetSoldBeforeFinishing([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var sold = await _db.StockMovements
        .Where(x =>
            x.IsActive &&
            x.BatchProduct_ID == batchProductId &&
            x.Move_Type == MoveType.OUT)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    return Ok(sold);
}

// GET: api/stockmovements/sold-by-batch?batchProductId=123
[HttpGet("sold-by-batch")]
public async Task<IActionResult> GetSoldByBatch([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var sold = await _db.StockMovements
        .Where(x =>
            x.IsActive &&
            x.BatchProduct_ID == batchProductId &&
            x.Move_Type == MoveType.OUT)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    return Ok(Math.Abs(sold));
}

// GET: api/stockmovements/sold-by-batchproduct?batchProductId=479
[HttpGet("sold-by-batchproduct")]
public async Task<IActionResult> GetSoldByBatchProduct(
    [FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return Ok(0);

    var sold = await _db.StockMovements
        .Where(x =>
            x.IsActive &&
            x.BatchProduct_ID == batchProductId &&
            x.Move_Type == MoveType.OUT)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    // OUT parasti ir pozitīvs, bet drošībai:
    return Ok(Math.Abs(sold));
}


   }
    
    
}
