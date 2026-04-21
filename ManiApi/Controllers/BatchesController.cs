// Šis kontrolieris ir paredzēts partiju (batch) pārvaldībai: izveidei, rediģēšanai, dzēšanai, kā arī partiju saraksta un detaļas skatīšanai.

using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
using MySqlConnector;
namespace ManiApi.Controllers

{
    [ApiController]
    [Route("api/[controller]")]
    public class BatchesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public BatchesController(AppDbContext db) => _db = db;

// POST: /api/batches/draft/create
[HttpPost("draft/create")]
    public async Task<IActionResult> CreateDraft([FromBody] BatchCartModel dto)
        {
            if (dto is null) return BadRequest("Tukšs pieprasījums.");
            var code = (dto.Title ?? "").Trim();

// ✅ JA MELNRAKSTS UN NAV NOSAUKUMA → tiek ģenerēts automātiski
if (string.IsNullOrWhiteSpace(code))
{
    code = "__DRAFT__" + Guid.NewGuid().ToString("N")[..8];
}

if (dto.Items is null || dto.Items.Count == 0)
{
    return BadRequest("Nav nevienas produkta rindas.");
}

// DB savienojums + transakcija
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

await using var tx = await conn.BeginTransactionAsync();

int? parentId = null;

// 🔒 VALIDĀCIJA (tā pati kā CreateDraft)
if (dto.Items is not null)
{
    foreach (var it in dto.Items)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = @"
SELECT 
    COUNT(ptp.ID) AS TopPartsCnt,
    SUM(
        CASE 
            WHEN NOT EXISTS (
                SELECT 1 
                FROM toppartsteps ts
                WHERE ts.ProductToPart_ID = ptp.ID
                  AND ts.IsActive = 1
            )
            THEN 1 ELSE 0 
        END
    ) AS InvalidCnt
FROM producttopparts ptp
WHERE ptp.Version_ID = @vid
  AND ptp.IsActive = 1;
";

        var p = cmd.CreateParameter();
        p.ParameterName = "@vid";
        p.Value = it.VersionId;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync();

        int topPartsCnt = 0;
        int invalidCnt = 0;

        if (await reader.ReadAsync())
        {
            topPartsCnt = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            invalidCnt  = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        }

        await reader.CloseAsync();

        if (topPartsCnt == 0)
        {
            await tx.RollbackAsync();
            return BadRequest($"Produktam (VersionId={it.VersionId}) nav neviena TopPart.");
        }

        if (invalidCnt > 0)
        {
            await tx.RollbackAsync();
            return BadRequest($"Produktam (VersionId={it.VersionId}) ir TopPart bez STEP.");
        }
    }
}           
            // 1) Unikuma pārbaude (starp VISIEM statusiem)
            await using (var chk = conn.CreateCommand())
            {
                chk.Transaction = tx;
                chk.CommandText = @"SELECT COUNT(*) FROM batches WHERE Batches_Code = @code;";
                var p = chk.CreateParameter(); p.ParameterName = "@code"; p.Value = code; chk.Parameters.Add(p);
                var cnt = Convert.ToInt32(await chk.ExecuteScalarAsync());
                if (cnt > 0)
                {
                    await tx.RollbackAsync();
                    return Conflict("Nosaukums (Title) jau eksistē. Izvēlies citu.");
                }
            }

            // 2) Header INSERT (statuss = 4 – melnraksts)
            int batchId;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO batches (Batches_Code, Batches_Statuss, Batches_StartDate, Batches_EndDate, Comments, IsActive)
VALUES (@code, 4, NULL, NULL, @comment, 1);
SELECT LAST_INSERT_ID();";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@code"; p1.Value = code; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@comment"; p2.Value = (object?)dto.Comment ?? DBNull.Value; cmd.Parameters.Add(p2);

                var obj = await cmd.ExecuteScalarAsync();
                batchId = Convert.ToInt32(obj);
            }

            // 3) Rindas (UPSERT pēc (Batch_Id, Version_Id))
if (dto.Items is not null)
{
    
foreach (var it in dto.Items)
{
       
    // ✅ JA IR izvēlēti child (TopParts)
    await using var row = conn.CreateCommand();
row.Transaction = tx;

row.CommandText = @"
INSERT INTO batches_products
    (Batch_Id, Version_Id, ProductToPart_ID, Planned_Qty, Done_Qty, Priority, BatchProduct_Comments, IsActive, ParentBatchProduct_ID)
VALUES
    (@bid, @vid, @ptpId, @qty, 0, 0, @comment, 1, @parentId);
SELECT LAST_INSERT_ID();";

var pb = row.CreateParameter();
pb.ParameterName = "@bid";
pb.Value = batchId;
row.Parameters.Add(pb);

var pv = row.CreateParameter();
pv.ParameterName = "@vid";
pv.Value = it.VersionId;
row.Parameters.Add(pv);

var pq = row.CreateParameter();
pq.ParameterName = "@qty";
pq.Value = it.Qty;
row.Parameters.Add(pq);

var pc = row.CreateParameter();
pc.ParameterName = "@comment";
pc.Value = (object?)it.Comment ?? DBNull.Value;
row.Parameters.Add(pc);

var p3 = row.CreateParameter();
p3.ParameterName = "@ptpId";
p3.Value = (object?)it.ProductToPartId ?? DBNull.Value;
row.Parameters.Add(p3);

row.CommandText = @"
INSERT INTO batches_products
    (Batch_Id, Version_Id, ProductToPart_ID, Planned_Qty, Done_Qty, Priority, BatchProduct_Comments, IsActive, ParentBatchProduct_ID)
VALUES
    (@bid, @vid, @ptpId, @qty, 0, 0, @comment, 1, @parentId);
SELECT LAST_INSERT_ID();";

var pParent = row.CreateParameter();
pParent.ParameterName = "@parentId";
pParent.Value = (object?)parentId ?? DBNull.Value;
row.Parameters.Add(pParent);

var newIdObj = await row.ExecuteScalarAsync();
var newId = Convert.ToInt32(newIdObj);

// JA vēl nav parent → šis kļūst par root
if (parentId == null)
{
    parentId = newId;
}
        }
}


            await tx.CommitAsync();
            return Ok(new { batchId });
        }

[HttpGet("check-code")]
public async Task<IActionResult> CheckCode([FromQuery] string code)
{
    if (string.IsNullOrWhiteSpace(code))
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COUNT(*)
FROM batches
WHERE Batches_Code = @code
  AND IsActive = 1;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@code";
    p.Value = code;
    cmd.Parameters.Add(p);

    var cnt = Convert.ToInt32(await cmd.ExecuteScalarAsync());

    return cnt > 0 ? Conflict() : Ok();
}


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBatch(int id, [FromQuery] string? reason = null)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // 1) Atzīmējam header kā neaktīvu un iestatām “Dzēsts” statusu (5)
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE batches 
   SET IsActive = 0,
       Batches_Statuss = 5,
       Comments = CASE 
                    WHEN @reason IS NULL OR @reason = '' THEN Comments
                    ELSE CONCAT(
                           COALESCE(Comments, ''), 
                           CASE WHEN LENGTH(COALESCE(Comments,''))>0 THEN ' | ' ELSE '' END,
                           @stamp, ' – ', @reason)
                  END
 WHERE ID = @id;";
                var pId = cmd.CreateParameter(); pId.ParameterName = "@id"; pId.Value = id; cmd.Parameters.Add(pId);
                var pReason = cmd.CreateParameter(); pReason.ParameterName = "@reason"; pReason.Value = (object?)reason ?? DBNull.Value; cmd.Parameters.Add(pReason);
                var pStamp = cmd.CreateParameter(); pStamp.ParameterName = "@stamp"; pStamp.Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"); cmd.Parameters.Add(pStamp);
                await cmd.ExecuteNonQueryAsync();
            }


            // 2) Atzīmējam visas rindas kā neaktīvas
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE batches_products SET IsActive = 0 WHERE Batch_Id = @id;";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new { batchId = id, deleted = true });
        }

        [HttpDelete("{batchId}/line/{versionId}")]
        public async Task<IActionResult> DeleteBatchLine(int batchId, int versionId)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE batches_products
   SET IsActive = 0
 WHERE Batch_Id = @bid AND Version_Id = @vid;";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@bid"; p1.Value = batchId; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@vid"; p2.Value = versionId; cmd.Parameters.Add(p2);
                var affected = await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return Ok(new { batchId, versionId, affected });
            }
        }


        // POST: /api/batches/draft/update
        [HttpPost("draft/update")]
        public async Task<IActionResult> UpdateDraft([FromBody] BatchCartModel dto)
        {
            if (dto is null) return BadRequest("Tukšs pieprasījums.");
            if (!(dto.BatchId.HasValue && dto.BatchId > 0)) return BadRequest("BatchId ir obligāts.");

            var code = (dto.Title ?? "").Trim();

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // 1) Pārbaude: nosaukumu pēc pirmās saglabāšanas mainīt nedrīkst
            await using (var chkName = conn.CreateCommand())
            {
                chkName.Transaction = tx;
                chkName.CommandText = @"SELECT Batches_Code FROM batches WHERE ID = @id;";
                var p = chkName.CreateParameter(); p.ParameterName = "@id"; p.Value = dto.BatchId!.Value; chkName.Parameters.Add(p);
                var current = (await chkName.ExecuteScalarAsync())?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(code) &&
                    !string.Equals(current, code, StringComparison.Ordinal))
                {
                    await tx.RollbackAsync();
                    return Conflict("Nosaukumu pēc pirmās saglabāšanas mainīt nevar.");
                }

            }

            // 2) Header UPDATE (komentārs, statuss paliek melnraksts)
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE batches
   SET Comments = @comment,
       Batches_Statuss = 4,
       IsActive = 1
 WHERE ID = @id;";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@comment"; p1.Value = (object?)dto.Comment ?? DBNull.Value; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@id"; p2.Value = dto.BatchId!.Value; cmd.Parameters.Add(p2);
                await cmd.ExecuteNonQueryAsync();
            }

// 🛡️ drošības filtrs – tikai derīgie ieraksti
dto.Items = (dto.Items ?? new List<BatchCartItem>())
    .Where(x => x.Qty > 0)
    .ToList();
            // 3) Rindas (UPSERT pēc (Batch_Id, Version_Id))
if (dto.Items is not null && dto.Items.Count > 0)

{
    // 0️⃣ Deaktivējam VISAS rindas šim batch
await using (var clear = conn.CreateCommand())
{
    clear.Transaction = tx;
    clear.CommandText = @"
DELETE FROM batches_products
WHERE Batch_Id = @bid;";

    var p = clear.CreateParameter();
    p.ParameterName = "@bid";
    p.Value = dto.BatchId!.Value;
    clear.Parameters.Add(p);

    await clear.ExecuteNonQueryAsync();
}

    int? parentId = null;

    foreach (var it in dto.Items)
    {
        // ✅ Backend aizsardzība: nedrīkst mainīt produktu (VersionId)
        if (it.ItemId > 0)
        {
            await using var chk = conn.CreateCommand();
            chk.Transaction = tx;
            chk.CommandText = @"
                SELECT Version_Id 
                FROM batches_products 
                WHERE ID = @id AND IsActive = 1;";
            var pid = chk.CreateParameter();
            pid.ParameterName = "@id";
            pid.Value = it.ItemId;
            chk.Parameters.Add(pid);

            var obj = await chk.ExecuteScalarAsync();
            var oldVid = (obj == null || obj == DBNull.Value) ? 0 : Convert.ToInt32(obj);

            if (oldVid != it.VersionId)
                return BadRequest("Produkta maiņa nav atļauta rediģēšanas režīmā.");

                
        }

        await using var row = conn.CreateCommand();
        row.Transaction = tx;
   row.CommandText = @"
        INSERT INTO batches_products
            (Batch_Id, Version_Id, ProductToPart_ID, Planned_Qty, Done_Qty, Priority, BatchProduct_Comments, IsActive, ParentBatchProduct_ID)
        VALUES
            (@bid, @vid, @ptpId, @qty, 0, 0, @comment, 1, @parentId);
        SELECT LAST_INSERT_ID();";

        var pb = row.CreateParameter();
        pb.ParameterName = "@bid";
        pb.Value = dto.BatchId!.Value;
        row.Parameters.Add(pb);

        var pv = row.CreateParameter();
        pv.ParameterName = "@vid";
        pv.Value = it.VersionId;
        row.Parameters.Add(pv);

        var pq = row.CreateParameter();
        pq.ParameterName = "@qty";
        pq.Value = it.Qty;
        row.Parameters.Add(pq);

        var pc = row.CreateParameter();
        pc.ParameterName = "@comment";
        pc.Value = (object?)it.Comment ?? DBNull.Value;
        row.Parameters.Add(pc);

        var p3 = row.CreateParameter();
        p3.ParameterName = "@ptpId";
        p3.Value = (object?)it.ProductToPartId ?? DBNull.Value;
        row.Parameters.Add(p3);

        row.CommandText = @"
INSERT INTO batches_products
    (Batch_Id, Version_Id, ProductToPart_ID, Planned_Qty, Done_Qty, Priority, BatchProduct_Comments, IsActive, ParentBatchProduct_ID)
VALUES
    (@bid, @vid, @ptpId, @qty, 0, 0, @comment, 1, @parentId);
SELECT LAST_INSERT_ID();";

var pParent = row.CreateParameter();
pParent.ParameterName = "@parentId";
pParent.Value = (object?)parentId ?? DBNull.Value;
row.Parameters.Add(pParent);

var newIdObj = await row.ExecuteScalarAsync();
var newId = Convert.ToInt32(newIdObj);

// JA vēl nav parent → šis kļūst par root
if (parentId == null)
{
    parentId = newId;
}
    }
}

            await tx.CommitAsync();
            return Ok(new { batchId = dto.BatchId!.Value });
        }

[HttpGet("by-batchproduct")]
public async Task<IActionResult> GetByBatchProduct([FromQuery] int batchProductId)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    bp.ID           AS BatchProductId,
    bp.Version_Id   AS VersionId,
    p.Product_Code  AS ProductCode,
    p.Product_Name  AS ProductName,
    c.Category_Name AS CategoryName
FROM batches_products bp
JOIN versions v   ON v.ID = bp.Version_Id
JOIN products p   ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID
WHERE bp.ID = @id
  AND bp.IsActive = 1;";

    var pId = cmd.CreateParameter();
    pId.ParameterName = "@id";
    pId.Value = batchProductId;
    cmd.Parameters.Add(pId);

    await using var r = await cmd.ExecuteReaderAsync();

    if (!await r.ReadAsync())
        return NotFound();

    return Ok(new
    {
        BatchProductId = r.GetInt32(0),
        VersionId      = r.GetInt32(1),
        ProductCode    = r.GetString(2),
        ProductName    = r.GetString(3),
        CategoryName   = r.GetString(4)
    });
}

// GET: /api/batches/by-id?batchProductId=513
[HttpGet("by-id")]
public async Task<IActionResult> GetByBatchProductId([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    bp.ID           AS BatchProductId,
    bp.Batch_Id     AS BatchId,
    bp.Version_Id   AS VersionId,
    p.Product_Code  AS ProductCode,
    c.Category_Name AS CategoryName
FROM batches_products bp
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID
WHERE bp.ID = @id
  AND bp.IsActive = 1
LIMIT 1;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@id";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    await using var r = await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync())
        return NotFound();

    return Ok(new
    {
        BatchProductId = r.GetInt32(0),
        BatchId        = r.GetInt32(1),
        VersionId      = r.GetInt32(2),
        ProductCode    = r.GetString(3),
        CategoryName   = r.GetString(4)
    });
}


// GET: /api/batches/list?batch_type=1
[HttpGet("list")]
public async Task<IActionResult> GetProductionBatches(
    [FromQuery] int batch_type = 1,
    [FromQuery] List<int>? versionIds = null)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    b.ID                               AS BatchId,
    b.Batches_Code                     AS BatchCode,
    MIN(bp.ID)                      AS BatchProductId,
    bp.Version_Id                      AS VersionId,
    p.Product_Name                     AS ProductName,
    p.Product_Code                     AS ProductCode,
    c.Category_Name                    AS CategoryName,
    v.Version_Name                     AS VersionName,
    bp.is_priority                     AS IsPriority,

   -- Planned: tikai 1/5, nav 2/3
SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS Planned,

-- Detailed IN PROGRESS:
-- ir vismaz viens Detailed ar 2 VAI 3
-- UN ir vēl kāds Detailed ar 1/2/5 (tātad nav 100% pabeigts)
SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
         AND EXISTS (
             SELECT 1
             FROM tasks t2
             JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
             WHERE t2.BatchProduct_ID = bp.ID
               AND ts2.Step_Type = 1
               AND t2.Tasks_Status IN (2,3)
         )
         AND EXISTS (
             SELECT 1
             FROM tasks t3
             JOIN toppartsteps ts3 ON ts3.ID = t3.TopPartStep_ID
             WHERE t3.BatchProduct_ID = bp.ID
               AND ts3.Step_Type = 1
               AND t3.Tasks_Status <> 3
         )
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS DetailedInProgress,

    -- Detailed FINISH:
    -- visi Detailed = 3, nav vairs 1/2/5
    -- UN Assembly vēl NAV sācies (nav statusu 2 vai 3)
SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
         AND EXISTS (
             SELECT 1
             FROM tasks t2
             JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
             WHERE t2.BatchProduct_ID = bp.ID
               AND ts2.Step_Type = 1
               AND t2.Tasks_Status IN (2,3)
         )
         AND NOT EXISTS (
             SELECT 1
             FROM tasks t3
             JOIN toppartsteps ts3 ON ts3.ID = t3.TopPartStep_ID
             WHERE t3.BatchProduct_ID = bp.ID
               AND ts3.Step_Type = 1
               AND t3.Tasks_Status <> 3
         )
         AND NOT EXISTS (
             SELECT 1
             FROM tasks t4
             JOIN toppartsteps ts4 ON ts4.ID = t4.TopPartStep_ID
             WHERE t4.BatchProduct_ID = bp.ID
               AND ts4.Step_Type = 2
               AND t4.Tasks_Status IN (2,3)
         )
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS DetailedFinish,

    -- Assembly IN PROGRESS:
    -- Ir vismaz viens Assembly ar 2 VAI 3
    -- UN ir vēl kāds Assembly ar 1/2/5 (nav 100% pabeigts)
SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
         AND EXISTS (
             SELECT 1
             FROM tasks t2
             JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
             WHERE t2.BatchProduct_ID = bp.ID
               AND ts2.Step_Type = 2
               AND t2.Tasks_Status IN (2,3)
         )
         AND EXISTS (
             SELECT 1
             FROM tasks t3
             JOIN toppartsteps ts3 ON ts3.ID = t3.TopPartStep_ID
             WHERE t3.BatchProduct_ID = bp.ID
               AND ts3.Step_Type = 2
               AND t3.Tasks_Status IN (1,2,5)
         )
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS Assembly,

    -- Assembly FINISH:
    -- ir vismaz viens Assembly ar 3
    -- UN vairs nav neviena Assembly ar 1/2/5
-- Assembly FINISH:
SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
        THEN COALESCE(sm.AssemblyDone, 0)
        ELSE 0
    END
) AS Done,

SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
        THEN COALESCE(sm.AssemblyDone, 0)
        ELSE 0
    END
)
-
SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
        THEN COALESCE(tsum.FinishingStartedQty, 0)
        ELSE 0
    END
) AS AssemblyFinish,

SUM(
    CASE
        WHEN bp.ProductToPart_ID IS NULL
        THEN COALESCE(tsum.FinishingStartedQty, 0)
        ELSE 0
    END
) AS FinishingInProgress

FROM batches_products bp
JOIN batches  b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p   ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID AND c.IsActive = 1

LEFT JOIN (
    SELECT
        BatchProduct_ID,
        SUM(CASE WHEN Move_Type = 'ASSEMBLY' THEN Stock_Qty ELSE 0 END)
        -
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END)
        AS AssemblyDone
    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) sm ON sm.BatchProduct_ID = bp.ID

LEFT JOIN (
    SELECT
        BatchProduct_ID,
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS Sold
    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) ss ON ss.BatchProduct_ID = bp.ID
LEFT JOIN (
    SELECT
        t.BatchProduct_ID,

        -- Detailed
        SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (1,5) THEN 1 ELSE 0 END) AS DetPlannedCnt,
        SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (2,3) THEN 1 ELSE 0 END) AS DetStartedCnt,
        SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) AS DetNotFinishedCnt,

        -- Assembly
        SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (2,3) THEN 1 ELSE 0 END) AS AsmStartedCnt,
        SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (1,2,5) THEN 1 ELSE 0 END) AS AsmNotFinishedCnt,

        -- Finishing
        -- Finishing (FIXED: izmanto Qty_Done, nevis count)
        SUM(
            CASE 
                WHEN ts.Step_Type = 3 
                AND t.Tasks_Status IN (1,2)
                THEN t.Qty_Done
                ELSE 0 
            END
        ) AS FinishingStartedQty

    FROM tasks t
    JOIN toppartsteps ts 
    ON ts.ID = t.TopPartStep_ID
    AND ts.IsActive = 1
    AND t.IsActive = 1
    WHERE t.IsActive = 1
    GROUP BY t.BatchProduct_ID
) tsum ON tsum.BatchProduct_ID = bp.ID

WHERE
    bp.IsActive = 1
    AND b.IsActive = 1
    AND b.Batches_Statuss = 1
    AND (@useFilter = 0 OR bp.Version_Id IN ({versionFilter}))
    -- ja tev IR kolonna, kas atbilst batch_type (piem. b.Batches_Type_Id),
    -- te vari pielikt filtru, piem.:
    -- AND b.Batches_Type_Id = @batchType
GROUP BY
    bp.Version_Id,
    p.Product_Name,
    p.Product_Code
ORDER BY
    p.Product_Name;";

var useFilter = versionIds != null && versionIds.Any();

if (useFilter)
{
    var paramNames = new List<string>();

    for (int i = 0; i < versionIds!.Count; i++)
    {
        var paramName = $"@v{i}";
        paramNames.Add(paramName);

        var p = cmd.CreateParameter();
        p.ParameterName = paramName;
        p.Value = versionIds[i];
        cmd.Parameters.Add(p);
    }

    var inClause = string.Join(",", paramNames);
    cmd.CommandText = cmd.CommandText.Replace("{versionFilter}", inClause);
}
else
{
    cmd.CommandText = cmd.CommandText.Replace("{versionFilter}", "NULL");
}

var pFilter = cmd.CreateParameter();
pFilter.ParameterName = "@useFilter";
pFilter.Value = useFilter ? 1 : 0;
cmd.Parameters.Add(pFilter);
    // ja izmanto filtru pēc tipa, atkomentē šo:
    // cmd.Parameters.Add(new MySqlParameter("@batchType", batch_type));

    var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
list.Add(new
{
    BatchId             = r.GetInt32(0),
    BatchCode           = r.GetString(1),
    BatchProductId      = r.GetInt32(2),
    VersionId           = r.GetInt32(3),
    ProductName         = r.GetString(4),
    ProductCode         = r.GetString(5),
    CategoryName        = r.GetString(6),
    VersionName         = r.GetString(7),
    IsPriority          = r.GetBoolean(8),
    Planned             = r.GetInt32(9),
    DetailedInProgress  = r.GetInt32(10),
    DetailedFinish      = r.GetInt32(11),
    Assembly            = r.GetInt32(12),
    Done                = r.GetInt32(13),
    AssemblyFinish      = r.GetInt32(14),
    FinishingInProgress = r.GetInt32(15)
});

    }

    return Ok(list);
}

// POST: /api/batches/planned
// body: { "batchId": 57, "code": "RP-2026-001" }
[HttpPost("planned")]
public async Task<IActionResult> SetPlanned([FromBody] SetPlannedDto? dto)
{
    if (dto is null)
        return BadRequest("Body is required.");

    if (dto.BatchId <= 0)
        return BadRequest("BatchId is required.");

    if (string.IsNullOrWhiteSpace(dto.Code))
        return BadRequest("Code is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1️⃣ Pārslēdzam header uz Planned (1) + iestatām KODU
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
UPDATE batches
   SET Batches_Statuss = 1,
       Batches_Code    = @code
 WHERE ID = @bid
   AND IsActive = 1;";

        var pBid = cmd.CreateParameter();
        pBid.ParameterName = "@bid";
        pBid.Value = dto.BatchId;
        cmd.Parameters.Add(pBid);

        var pCode = cmd.CreateParameter();
        pCode.ParameterName = "@code";
        pCode.Value = dto.Code.Trim();
        cmd.Parameters.Add(pCode);

        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected == 0)
            return NotFound("Batch not found or inactive.");
    }

    // 2️⃣ Ģenerējam tasks visiem soļiem šai partijai (statuss = 5)
    int tasksCreated;

    await using (var tcmd = conn.CreateCommand())
    {
        tcmd.CommandText = @"
INSERT INTO tasks
    (BatchProduct_ID, TopPartStep_ID, Tasks_Status,
     Tasks_Priority, Qty_Done, Qty_Scrap,
     Assigned_To, Claimed_By, IsActive)
SELECT
    bp.ID,
    ts.ID,
    5,
    0,
    0,
    0,
    NULL,
    NULL,
    1
FROM batches_products bp
JOIN producttopparts ptp
     ON ptp.Version_ID = bp.Version_Id
    AND ptp.IsActive = 1
JOIN toppartsteps ts
     ON ts.ProductToPart_ID = ptp.ID
    AND ts.IsActive = 1
    AND (
        (bp.ProductToPart_ID IS NULL AND ts.Step_Type IN (1,2))
        OR
        (bp.ProductToPart_ID IS NOT NULL AND ts.Step_Type = 1)
    )
LEFT JOIN tasks t
     ON t.BatchProduct_ID = bp.ID
    AND t.TopPartStep_ID = ts.ID
    AND t.IsActive = 1
WHERE bp.Batch_Id = @bid
  AND bp.IsActive = 1
  AND ts.IsActive = 1
  AND (
    bp.ProductToPart_ID IS NULL
    OR ptp.ID = bp.ProductToPart_ID
        )
  AND t.ID IS NULL;";

        var pbid = tcmd.CreateParameter();
        pbid.ParameterName = "@bid";
        pbid.Value = dto.BatchId;
        tcmd.Parameters.Add(pbid);

        tasksCreated = await tcmd.ExecuteNonQueryAsync();
    }

    // 3️⃣ STOCK_MOVEMENTS: -PLANNED (rezervējam apjomu)
await using (var smCmd = conn.CreateCommand())
{
    smCmd.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, IsActive)
SELECT
    bp.Version_Id,
    bp.ID,
    'PLANNED',
    -bp.Planned_Qty,
    UTC_TIMESTAMP(),
    1
FROM batches_products bp
WHERE bp.Batch_Id = @bid
  AND bp.IsActive = 1;";

    var pBid2 = smCmd.CreateParameter();
    pBid2.ParameterName = "@bid";
    pBid2.Value = dto.BatchId;
    smCmd.Parameters.Add(pBid2);

    await smCmd.ExecuteNonQueryAsync();
}

    return Ok(new
    {
        batchId = dto.BatchId,
        status = 1,
        tasksCreated
    });

}

[HttpPost("draft/delete")]
public async Task<IActionResult> DeleteDraft([FromBody] DeleteDraftDto dto)
{
    if (dto.BatchId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1️⃣ soft delete batch items
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
UPDATE batches_products
   SET IsActive = 0
 WHERE Batch_Id = @bid AND IsActive = 1;
";
        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = dto.BatchId;
        cmd.Parameters.Add(p);

        await cmd.ExecuteNonQueryAsync();
    }

    // 2️⃣ batch status → 5
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
UPDATE batches
   SET Batches_Statuss = 5
 WHERE ID = @bid AND IsActive = 1;
";
        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = dto.BatchId;
        cmd.Parameters.Add(p);

        await cmd.ExecuteNonQueryAsync();
    }

    return Ok();
}

public sealed class DeleteDraftDto
{
    public int BatchId { get; set; }
}


[HttpGet("by-version")]
public async Task<IActionResult> GetByVersion([FromQuery] int versionId, [FromQuery] int batch_type = 1)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
  b.ID           AS BatchId,
  b.Batches_Code AS BatchCode,
  bp.ID          AS BatchProductId,
  bp.Version_Id  AS VersionId,

  bp.Planned_Qty AS Planned,

  -- ✅ Stage balances (nekad nerādām mīnusu)
  GREATEST(COALESCE(s.Detailed,  0), 0) AS Detailed,
  GREATEST(COALESCE(s.Assembly,  0), 0) AS Assembly,
  GREATEST(COALESCE(s.Finishing, 0), 0) AS Finishing,
  GREATEST(COALESCE(s.Stock,     0), 0) AS Done,

  b.Comments AS Comment,
  (
      SELECT MIN(t.Started_At)
      FROM tasks t
      WHERE t.BatchProduct_ID = bp.ID
        AND t.IsActive = 1
        AND t.Started_At IS NOT NULL
  ) AS StartedAt,

  b.Batches_Statuss AS BatchStatus

FROM batches b
JOIN batches_products bp 
      ON b.ID = bp.Batch_Id 
     AND bp.IsActive = 1

LEFT JOIN (
    SELECT 
      sm.BatchProduct_ID AS BatchProductId,

      SUM(CASE WHEN sm.Move_Type = 'DETAILED'  THEN sm.Stock_Qty ELSE 0 END) AS Detailed,
      SUM(CASE WHEN sm.Move_Type = 'ASSEMBLY'  THEN sm.Stock_Qty ELSE 0 END) AS Assembly,
      SUM(CASE WHEN sm.Move_Type = 'FINISHING' THEN sm.Stock_Qty ELSE 0 END) AS Finishing,
      SUM(CASE WHEN sm.Move_Type = 'STOCK'     THEN sm.Stock_Qty ELSE 0 END) AS Stock

    FROM stock_movements sm
    WHERE sm.IsActive = 1
    GROUP BY sm.BatchProduct_ID
) s ON s.BatchProductId = bp.ID

WHERE bp.Version_Id      = @vid
  AND b.Batches_Statuss  = @type
  AND b.IsActive         = 1
ORDER BY b.ID DESC;
";

    var pVid = cmd.CreateParameter();
pVid.ParameterName = "@vid";
pVid.Value = versionId;
cmd.Parameters.Add(pVid);

var pTyp = cmd.CreateParameter();
pTyp.ParameterName = "@type";
pTyp.Value = batch_type;
cmd.Parameters.Add(pTyp);


    var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        list.Add(new
        {
            BatchId        = r.GetInt32(0),
            BatchCode      = r.GetString(1),
            BatchProductId = r.GetInt32(2),
            Version_Id     = r.GetInt32(3),

            Planned   = r.GetInt32(4),
            Detailed  = r.GetInt32(5),
            Assembly  = r.GetInt32(6),
            Finishing = r.GetInt32(7),
            Done      = r.GetInt32(8),

            Comment     = r.IsDBNull(9)  ? null : r.GetString(9),
            StartedAt   = r.IsDBNull(10) ? (DateTime?)null : r.GetDateTime(10),
            BatchStatus = r.GetInt32(11)
        });
    }

    return Ok(list);
}


[HttpPost("update-batchproduct")]
public async Task<IActionResult> UpdateBatchProduct([FromBody] UpdateBatchProductDto dto)
{
    if (dto.BatchProductId <= 0)
        return BadRequest("BatchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1) Nolasa statusu un StartedAt šai rindai
    int status;
    DateTime? startedAt;

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT 
    b.Batches_Statuss,
    (
        SELECT MIN(t.Started_At)
        FROM tasks t
        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1
    ) AS StartedAt
FROM batches_products bp
JOIN batches b ON b.ID = bp.Batch_Id
WHERE bp.ID = @id
  AND bp.IsActive = 1;";

        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = dto.BatchProductId;
        cmd.Parameters.Add(p);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync())
            return NotFound("BatchProduct not found.");

        status    = r.GetInt32(0);
        startedAt = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1);
    }

    // 2) Loģika: qty drīkst mainīt tikai, ja:
    //    - statuss = 5 (VISIS)  VAI
    //    - nav StartedAt
    var canEditQty = (status == 5) || (startedAt == null);

    // 3) Atjaunojam
    await using (var upd = conn.CreateCommand())
    {
        upd.CommandText = @"
UPDATE batches_products
SET 
    Planned_Qty = CASE WHEN @canEdit = 1 THEN @qty ELSE Planned_Qty END,
    BatchProduct_Comments = @comment
WHERE ID = @id
  AND IsActive = 1;";

        var pid = upd.CreateParameter();
        pid.ParameterName = "@id";
        pid.Value = dto.BatchProductId;
        upd.Parameters.Add(pid);

        var pq = upd.CreateParameter();
        pq.ParameterName = "@qty";
        pq.Value = dto.PlannedQty;
        upd.Parameters.Add(pq);

        var pc = upd.CreateParameter();
        pc.ParameterName = "@comment";
        pc.Value = (object?)dto.Comment ?? DBNull.Value;
        upd.Parameters.Add(pc);

        var pce = upd.CreateParameter();
        pce.ParameterName = "@canEdit";
        pce.Value = canEditQty ? 1 : 0;
        upd.Parameters.Add(pce);

        await upd.ExecuteNonQueryAsync();
    }

    // ŠEIT GALVENĀ IZMAIŅA:
    // vairs nemet 409 – atgriežam info, kas notika
    return Ok(new 
    { 
        Ok = true, 
        QuantityChanged = canEditQty 
    });
}

[HttpGet("draft/last")]
public async Task<IActionResult> GetLastDraft()
{
    var cs = _db.Database.GetConnectionString();
    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync();

    // 1) atrodam vienīgo aktīvo melnrakstu
    int? batchId = null;
    string? comment = null;

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT ID, Comments
FROM batches
WHERE Batches_Statuss = 4
  AND IsActive = 1
LIMIT 1;
";
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            batchId = reader.GetInt32(0);
            comment = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
    }

    if (!batchId.HasValue)
        return NoContent(); // melnraksta nav

    // 2) atrodam melnraksta preces
    var items = new List<object>();

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT Version_Id, Planned_Qty, BatchProduct_Comments, ProductToPart_ID
FROM batches_products
WHERE Batch_Id = @bid
  AND IsActive = 1;
";
        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = batchId.Value;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                VersionId = reader.GetInt32(0),
                Qty       = reader.GetInt32(1),
                Comment   = reader.IsDBNull(2) ? null : reader.GetString(2),
                ProductToPartId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)
            });
        }
    }

    // 3) atgriežam rezultātu
    return Ok(new
    {
        BatchId = batchId.Value,
        Comment = comment,
        Items   = items
    });
}



 // POST: /api/batches/update-line
[HttpPost("update-line")]
public async Task<IActionResult> UpdateLine([FromBody] UpdateBatchProductDto? dto)
{
    if (dto is null)
        return BadRequest("Body is required.");
    if (dto.BatchProductId <= 0)
        return BadRequest("BatchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    // 1) Nolasām pašreizējo Planned_Qty
    int currentQty;
    await using (var cmdSel = conn.CreateCommand())
    {
        cmdSel.Transaction = tx;
        cmdSel.CommandText = @"
SELECT Planned_Qty
FROM batches_products
WHERE ID = @id AND IsActive = 1;";
        var p = cmdSel.CreateParameter();
        p.ParameterName = "@id";
        p.Value = dto.BatchProductId;
        cmdSel.Parameters.Add(p);

        var obj = await cmdSel.ExecuteScalarAsync();
        if (obj is null || obj == DBNull.Value)
        {
            await tx.RollbackAsync();
            return NotFound("Batch product not found.");
        }

        currentQty = Convert.ToInt32(obj);
    }

    // 2) Ja mēģina mainīt daudzumu – pārbaudām, vai darbs nav sācies
    if (dto.PlannedQty != currentQty)
    {
        await using var cmdChk = conn.CreateCommand();
        cmdChk.Transaction = tx;
        cmdChk.CommandText = @"
SELECT COUNT(*)
FROM tasks t
WHERE t.BatchProduct_ID = @bpId
  AND t.IsActive = 1
  AND t.Tasks_Status IN (1,2,3);";
        var p2 = cmdChk.CreateParameter();
        p2.ParameterName = "@bpId";
        p2.Value = dto.BatchProductId;
        cmdChk.Parameters.Add(p2);

        var cnt = Convert.ToInt32(await cmdChk.ExecuteScalarAsync());
        if (cnt > 0)
        {
            await tx.RollbackAsync();
            return BadRequest("Daudzumu nevar mainīt, jo darbs jau ir uzsākts.");
        }
    }

    // 3) Atjaunojam Planned_Qty + komentāru
    await using (var cmdUpd = conn.CreateCommand())
    {
        cmdUpd.Transaction = tx;
        cmdUpd.CommandText = @"
UPDATE batches_products
   SET Planned_Qty = @qty,
       BatchProduct_Comments = @comment
 WHERE ID = @id;";

        var pId = cmdUpd.CreateParameter();
        pId.ParameterName = "@id";
        pId.Value = dto.BatchProductId;
        cmdUpd.Parameters.Add(pId);

        var pQty = cmdUpd.CreateParameter();
        pQty.ParameterName = "@qty";
        pQty.Value = dto.PlannedQty;
        cmdUpd.Parameters.Add(pQty);

        var pCom = cmdUpd.CreateParameter();
        pCom.ParameterName = "@comment";
        pCom.Value = (object?)dto.Comment ?? DBNull.Value;
        cmdUpd.Parameters.Add(pCom);

        await cmdUpd.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();

    return Ok(new
    {
        batchProductId = dto.BatchProductId,
        plannedQty = dto.PlannedQty,
        comment = dto.Comment
    });
}

// POST: /api/batches/update-comment
[HttpPost("update-comment")]
public async Task<IActionResult> UpdateBatchComment([FromBody] UpdateBatchCommentDto dto)
{
    if (dto.BatchId <= 0)
        return BadRequest("BatchId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
UPDATE batches
SET Comments = @comment
WHERE ID = @id
  AND IsActive = 1;";

    var pId = cmd.CreateParameter();
    pId.ParameterName = "@id";
    pId.Value = dto.BatchId;
    cmd.Parameters.Add(pId);

    var pCom = cmd.CreateParameter();
    pCom.ParameterName = "@comment";
    pCom.Value = (object?)dto.Comment ?? DBNull.Value;
    cmd.Parameters.Add(pCom);

    await cmd.ExecuteNonQueryAsync();

    return Ok(new { batchId = dto.BatchId });
}

[HttpGet("list-production")]
public async Task<IActionResult> GetProductionBatchesRows()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    b.ID           AS BatchId,
    b.Batches_Code AS BatchCode,
    bp.ID          AS BatchProductId,
    bp.Version_Id  AS VersionId,
    p.Product_Name AS ProductName,
    p.Product_Code AS ProductCode,
    c.Category_Name AS CategoryName,
    v.Version_Name  AS VersionName,
    bp.is_priority  AS IsPriority,
    bp.Planned_Qty  AS Planned, 
    bp.BatchProduct_Comments AS Comment,
    bp.ProductToPart_ID
, COALESCE(tsum.DetailedInProgress, 0)  AS DetailedInProgress
, COALESCE(tsum.DetailedFinish, 0)      AS DetailedFinish
, COALESCE(tsum.AssemblyInProgress, 0)  AS AssemblyInProgress
, COALESCE(sm.AssemblyDone, 0)      AS AssemblyFinish
, COALESCE(sm.Sold, 0)              AS Sold
, COALESCE(tsum.FinishingInProgress, 0) AS FinishingInProgress
, COALESCE(tsum.FinStatus1, 0) AS FinStatus1
, COALESCE(tsum.FinStatus2, 0) AS FinStatus2
, COALESCE(tsum.FinStatus3, 0) AS FinStatus3
, tsum.DetailStart
, tsum.DetailFinish
, tsum.AssemblyStart
, tsum.AssemblyFinishDate
, COALESCE(tsum.DetailedStarted, 0) AS DetailedStarted
, COALESCE(pp.DetailsTotal, 0) AS DetailsTotal
FROM batches_products bp
JOIN batches  b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID      
LEFT JOIN (
    SELECT 
        BatchProduct_ID,

SUM(CASE 
        WHEN Move_Type = 'ASSEMBLY' AND Stock_Qty > 0 
        THEN Stock_Qty 
        ELSE 0 
    END)
-
SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END)
AS AssemblyDone,

SUM(CASE WHEN Move_Type = 'FINISHING' THEN Stock_Qty ELSE 0 END) AS FinishingSent,

SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS Sold

    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) sm ON sm.BatchProduct_ID = bp.ID

LEFT JOIN (
    SELECT
        t.BatchProduct_ID,
        CASE
    WHEN SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (1,2,3) THEN 1 ELSE 0 END) > 0
     AND SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) > 0
    THEN MAX(bp2.Planned_Qty)
    ELSE 0
END AS DetailedInProgress,

CASE
    WHEN SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) = 0
    THEN MAX(bp2.Planned_Qty)
    ELSE 0
END AS DetailedFinish,

        CASE
            WHEN SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (2,3) THEN 1 ELSE 0 END) > 0
             AND SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (1,2,5) THEN 1 ELSE 0 END) > 0
            THEN MAX(bp2.Planned_Qty)
            ELSE 0
        END AS AssemblyInProgress,

SUM(CASE 
        WHEN ts.Step_Type = 3 AND t.Tasks_Status <> 5
        THEN t.Qty_Done 
        ELSE 0
    END) AS FinishingInProgress,
SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 1 THEN t.Qty_Done ELSE 0 END) AS FinStatus1,
SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 2 THEN t.Qty_Done ELSE 0 END) AS FinStatus2,
SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 3 THEN t.Qty_Done ELSE 0 END) AS FinStatus3,

MIN(CASE 
        WHEN ts.Step_Type = 1 
         AND t.Tasks_Status IN (2,3)
         AND t.Started_At IS NOT NULL
        THEN t.Started_At 
      END) AS DetailStart,

CASE
    WHEN SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) = 0
    THEN MAX(CASE 
        WHEN ts.Step_Type = 1 AND t.Tasks_Status = 3 
        THEN t.Finished_At 
    END)
    ELSE NULL
END AS DetailFinish,

MIN(CASE 
        WHEN ts.Step_Type = 2 AND t.Tasks_Status = 2
        THEN t.Started_At
      END) AS AssemblyStart,

MAX(CASE 
        WHEN ts.Step_Type = 2 AND t.Tasks_Status = 3
        THEN t.Finished_At
      END) AS AssemblyFinishDate,

COUNT(DISTINCT CASE 
      WHEN mdet.Stage = 1 AND t.Tasks_Status IN (1,2,3)
      THEN ptp.ID
  END) AS DetailedStarted
    FROM tasks t
    JOIN toppartsteps ts 
  ON ts.ID = t.TopPartStep_ID
 AND ts.IsActive = 1
    JOIN producttopparts ptp 
        ON ptp.ID = ts.ProductToPart_ID
        AND ptp.IsActive = 1
            LEFT JOIN stage_step_type_map mdet
            ON mdet.Stage = 1
            AND mdet.IsActive = 1
            AND mdet.Step_Type_ID = ts.Step_Type
JOIN batches_products bp2 ON bp2.ID = t.BatchProduct_ID
WHERE t.IsActive = 1
  AND ts.IsActive = 1
GROUP BY t.BatchProduct_ID
) tsum ON tsum.BatchProduct_ID = bp.ID
LEFT JOIN (
    SELECT
        ptp.Version_ID,
        COUNT(DISTINCT ptp.ID) AS DetailsTotal
    FROM producttopparts ptp
    JOIN toppartsteps ts
         ON ts.ProductToPart_ID = ptp.ID
        AND ts.IsActive = 1
    JOIN stage_step_type_map mdet
         ON mdet.Stage = 1
        AND mdet.IsActive = 1
        AND mdet.Step_Type_ID = ts.Step_Type
    WHERE ptp.IsActive = 1
    GROUP BY ptp.Version_ID
) pp ON pp.Version_ID = bp.Version_Id
WHERE bp.IsActive = 1
  AND b.IsActive = 1
  AND b.Batches_Statuss = 1
  AND bp.ParentBatchProduct_ID IS NULL
ORDER BY b.ID DESC;
";

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            BatchId        = r.GetInt32(0),
            BatchCode      = r.GetString(1),
            BatchProductId = r.GetInt32(2),
            VersionId      = r.GetInt32(3),
            ProductName    = r.GetString(4),
            ProductCode    = r.GetString(5),
            CategoryName   = r.GetString(6),
            VersionName    = r.GetString(7),
            IsPriority     = r.GetBoolean(8),
            Planned = r.GetInt32(9),
            Comment = r.IsDBNull(10) ? null : r.GetString(10),
            ProductToPartId = r.IsDBNull(11) ? null : (int?)r.GetInt32(11),
            DetailedInProgress = r.GetInt32(12),
            DetailedFinish      = r.GetInt32(13),
            AssemblyInProgress  = r.GetInt32(14),
            AssemblyFinish      = r.GetInt32(15),
            Sold                = r.GetInt32(16),
            FinishingInProgress = r.GetInt32(17),
            FinStatus1 = r.IsDBNull(18) ? 0 : r.GetInt32(18),
            FinStatus2 = r.IsDBNull(19) ? 0 : r.GetInt32(19),
            FinStatus3 = r.IsDBNull(20) ? 0 : r.GetInt32(20),

            DetailStart  = r.IsDBNull(21) ? (DateTime?)null : r.GetFieldValue<DateTime>(21),
            DetailFinish = r.IsDBNull(22) ? (DateTime?)null : r.GetFieldValue<DateTime>(22),

            AssemblyStart  = r.IsDBNull(23) ? (DateTime?)null : r.GetFieldValue<DateTime>(23),
            AssemblyFinishDate = r.IsDBNull(24) ? (DateTime?)null : r.GetFieldValue<DateTime>(24),

            DetailedStarted = r.GetInt32(25),
            DetailsTotal = r.GetInt32(26)
        });
    }

    return Ok(list);
}

// GET: /api/batches/assembly-out?batchProductId=123
[HttpGet("assembly-out")]
public async Task<IActionResult> GetAssemblyOut([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COALESCE(SUM(Stock_Qty),0)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'OUT'
  AND IsActive = 1
  AND Stock_Qty > 0;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var result = await cmd.ExecuteScalarAsync();
    var qty = (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);

    return Ok(qty);
}

// GET: /api/batches/finishing-out
[HttpGet("finishing-out")]
public async Task<IActionResult> GetFinishingOut([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COALESCE(SUM(Stock_Qty),0)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'FINISHING'
  AND IsActive = 1
  AND Stock_Qty < 0;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var result = await cmd.ExecuteScalarAsync();
    var qty = result == null ? 0 : Math.Abs(Convert.ToInt32(result));

    return Ok(qty);
}

// GET: api/batches/assembly-out-total?batchProductId=123
[HttpGet("assembly-out-total")]
public async Task<IActionResult> GetAssemblyOutTotal([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    COALESCE(SUM(CASE WHEN Move_Type='ASSEMBLY' THEN Stock_Qty END),0)
  - COALESCE(SUM(CASE WHEN Move_Type='OUT' THEN Stock_Qty END),0)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND IsActive = 1;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var result = await cmd.ExecuteScalarAsync();
    var qty = result == null ? 0 : Math.Abs(Convert.ToInt32(result));

    return Ok(qty);
}

[HttpGet("validate-for-planning")]
public async Task<IActionResult> ValidateForPlanning([FromQuery] int versionId)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    COUNT(ptp.ID) AS TopPartsCnt,

    SUM(
        CASE 
            WHEN NOT EXISTS (
                SELECT 1 
                FROM toppartsteps ts
                WHERE ts.ProductToPart_ID = ptp.ID
                  AND ts.IsActive = 1
            )
            THEN 1 ELSE 0 
        END
    ) AS InvalidCnt,

    -- ✅ ja NAV neviena step DETAIL stage
    SUM(CASE WHEN ts.Step_Type = 1 THEN 1 ELSE 0 END) AS DetailCnt,

    -- ✅ ja NAV neviena step ASSEMBLY stage
    SUM(CASE WHEN ts.Step_Type = 2 THEN 1 ELSE 0 END) AS AssemblyCnt,

    -- ✅ ja NAV neviena step FINISHING stage
    SUM(CASE WHEN ts.Step_Type = 3 THEN 1 ELSE 0 END) AS FinishingCnt

FROM producttopparts ptp

LEFT JOIN toppartsteps ts 
    ON ts.ProductToPart_ID = ptp.ID
   AND ts.IsActive = 1

WHERE ptp.Version_ID = @vid
  AND ptp.IsActive = 1;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@vid";
    p.Value = versionId;
    cmd.Parameters.Add(p);

    await using var r = await cmd.ExecuteReaderAsync();

    int topParts = 0;
    int invalid = 0;
    int detailCnt = 0;
    int assemblyCnt = 0;
    int finishingCnt = 0;

    if (await r.ReadAsync())
    {
        topParts = r.IsDBNull(0) ? 0 : r.GetInt32(0);
        invalid  = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        detailCnt = r.IsDBNull(2) ? 0 : r.GetInt32(2);
        assemblyCnt = r.IsDBNull(3) ? 0 : r.GetInt32(3);
        finishingCnt = r.IsDBNull(4) ? 0 : r.GetInt32(4);
    }

    if (topParts == 0)
    return Ok(new { isValid = false, message = "Nav nevienas Pamatdetaļas" });

if (invalid > 0)
    return Ok(new { isValid = false, message = "Ir Pamatdetaļa bez tehnoloģijas soļiem." });

if (detailCnt == 0)
    return Ok(new { isValid = false, message = "Nav DETAIL posmā tehnoloģijas soļu." });

if (assemblyCnt == 0)
    return Ok(new { isValid = false, message = "Nav ASSEMBLY posmā tehnoloģijas soļu." });

if (finishingCnt == 0)
    return Ok(new { isValid = false, message = "Nav FINISHING posmā tehnoloģijas soļu." });

    return Ok(new { isValid = true });
}

[HttpGet("batch-flags")]
public async Task<IActionResult> GetBatchFlags()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    Batch_Id,
    Version_Id,

    CASE 
        WHEN SUM(CASE WHEN ProductToPart_ID IS NULL THEN 1 ELSE 0 END) > 0
         AND SUM(CASE WHEN ProductToPart_ID IS NOT NULL THEN 1 ELSE 0 END) > 0
            THEN '+'
        
        WHEN SUM(CASE WHEN ProductToPart_ID IS NULL THEN 1 ELSE 0 END) = 0
            THEN '!'
        
        ELSE ''
    END AS BatchFlag

FROM batches_products
WHERE IsActive = 1
GROUP BY Batch_Id, Version_Id;
";

    var dict = new Dictionary<string, string>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        var key = $"{r.GetInt32(0)}_{r.GetInt32(1)}";
        var flag = r.IsDBNull(2) ? "" : r.GetString(2);

        dict[key] = flag;
    }

    return Ok(dict);
}

[HttpGet("list-production-v2")]
public async Task<IActionResult> GetProductionBatchesRowsV2()
{
    var conn = _db.Database.GetDbConnection();
await conn.OpenAsync();

await using var cmd = conn.CreateCommand();
cmd.CommandText = @"
SELECT
    b.ID AS BatchId,
    b.Batches_Code AS BatchCode,    
    bp.RootId,

    MIN(bp.RootId) AS BatchProductId,

    bp.Version_Id AS VersionId,
    p.Product_Name AS ProductName,
    p.Product_Code AS ProductCode,
    c.Category_Name AS CategoryName,
    v.Version_Name AS VersionName,

    MAX(bp.is_priority) AS IsPriority,
    MAX(CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END) AS HasParent,

    SUM(
        CASE 
            WHEN bp.ProductToPart_ID IS NULL 
            THEN bp.Planned_Qty 
            ELSE 0 
        END
    ) AS Planned,

    MAX(bp.BatchProduct_Comments) AS Comment

, SUM(
    CASE 
        WHEN bp.ProductToPart_ID IS NULL 
        THEN COALESCE(sm.Sold, 0)
        ELSE 0
    END
) AS Sold

, SUM(
    CASE 
        WHEN bp.ProductToPart_ID IS NULL 
        THEN COALESCE(sm.AssemblyDone, 0)
        ELSE 0
    END
) AS Done

, CASE 
    WHEN MAX(CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END) = 1
    THEN MAX(dt.DetailsTotal)
    ELSE 0
END AS DetailsTotal

, MAX(COALESCE(ch.ChildCount, 0)) AS DetailsChildTotal
, CASE 
    WHEN MAX(CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END) = 1
    THEN MAX(dstat.DetailsChildDone)
    ELSE 0
END AS DetailsDone
, COALESCE(dstat.DetailsChildDone,0) AS DetailsChildDone
, dtask.DetailStart
, dtask.DetailFinish

FROM (
    SELECT
        bp.*,
        CASE 
    WHEN EXISTS (
        SELECT 1
        FROM batches_products bp2
        WHERE bp2.Batch_Id = bp.Batch_Id
          AND bp2.Version_Id = bp.Version_Id
          AND bp2.ProductToPart_ID IS NULL
          AND bp2.IsActive = 1
    )
    THEN (
        SELECT bp2.ID
        FROM batches_products bp2
        WHERE bp2.Batch_Id = bp.Batch_Id
          AND bp2.Version_Id = bp.Version_Id
          AND bp2.ProductToPart_ID IS NULL
          AND bp2.IsActive = 1
        LIMIT 1
    )
    ELSE bp.ID
END AS RootId

    FROM batches_products bp
    WHERE bp.IsActive = 1
) bp
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID

LEFT JOIN (
    SELECT
        BatchProduct_ID,
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS Sold,
        SUM(CASE WHEN Move_Type = 'ASSEMBLY' THEN Stock_Qty ELSE 0 END)
        -
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS AssemblyDone
    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) sm ON sm.BatchProduct_ID = bp.ID
AND bp.ProductToPart_ID IS NULL

LEFT JOIN (
    SELECT
        v.ID AS VersionId,
        COUNT(DISTINCT ptp.ID) AS DetailsTotal
    FROM versions v
    JOIN producttopparts ptp 
        ON ptp.Version_ID = v.ID
        AND ptp.IsActive = 1
    JOIN toppartsteps ts
        ON ts.ProductToPart_ID = ptp.ID
        AND ts.IsActive = 1
    JOIN stage_step_type_map m
        ON m.Step_Type_ID = ts.Step_Type
        AND m.Stage = 1
        AND m.IsActive = 1
    WHERE ptp.IsActive = 1
    GROUP BY v.ID
) dt ON dt.VersionId = bp.Version_Id

LEFT JOIN (
    SELECT
        bp.Batch_Id,
        bp.Version_Id,
        COUNT(DISTINCT bp.ProductToPart_ID) AS ChildCount
    FROM batches_products bp
    WHERE bp.ProductToPart_ID IS NOT NULL
      AND bp.IsActive = 1
    GROUP BY bp.Batch_Id, bp.Version_Id
) ch 
ON ch.Batch_Id = bp.Batch_Id 
AND ch.Version_Id = bp.Version_Id

LEFT JOIN (
    SELECT
        bp.RootId,

        MIN(CASE 
            WHEN t.Tasks_Status IN (2,3) AND t.Started_At IS NOT NULL
            THEN t.Started_At 
        END) AS DetailStart,

        CASE
            WHEN COALESCE(SUM(CASE WHEN t.Tasks_Status IN (1,2,5) THEN 1 ELSE 0 END),0) = 0
            THEN MAX(t.Finished_At)
            ELSE NULL
        END AS DetailFinish

    FROM (
        SELECT
            bp.*,
            CASE 
                WHEN EXISTS (
                    SELECT 1
                    FROM batches_products bp2
                    WHERE bp2.Batch_Id = bp.Batch_Id
                      AND bp2.Version_Id = bp.Version_Id
                      AND bp2.ProductToPart_ID IS NULL
                      AND bp2.IsActive = 1
                )
                THEN (
                    SELECT bp2.ID
                    FROM batches_products bp2
                    WHERE bp2.Batch_Id = bp.Batch_Id
                      AND bp2.Version_Id = bp.Version_Id
                      AND bp2.ProductToPart_ID IS NULL
                      AND bp2.IsActive = 1
                    LIMIT 1
                )
                ELSE bp.ID
            END AS RootId
        FROM batches_products bp
        WHERE bp.IsActive = 1
    ) bp

    LEFT JOIN tasks t 
            ON t.BatchProduct_ID IN (
                SELECT bp2.ID
                FROM batches_products bp2
                WHERE bp2.Batch_Id = bp.Batch_Id
                AND bp2.Version_Id = bp.Version_Id
                AND bp2.IsActive = 1
            )
            AND t.IsActive = 1

    GROUP BY bp.RootId
) dtask ON dtask.RootId = bp.RootId

LEFT JOIN (
    SELECT
        bp.Batch_Id,
        bp.Version_Id,

        -- Parent gatavie (visi step=3)
SUM(
    CASE 
        WHEN bp.ProductToPart_ID IS NULL
         AND EXISTS (
             SELECT 1
             FROM batches_products bp2
             WHERE bp2.Batch_Id = bp.Batch_Id
               AND bp2.Version_Id = bp.Version_Id
               AND bp2.IsActive = 1
         )
         AND NOT EXISTS (
             SELECT 1
             FROM tasks t
             WHERE t.BatchProduct_ID IN (
                 SELECT bp2.ID
                 FROM batches_products bp2
                 WHERE bp2.Batch_Id = bp.Batch_Id
                   AND bp2.Version_Id = bp.Version_Id
                   AND bp2.IsActive = 1
             )
             AND t.IsActive = 1
             AND t.Tasks_Status <> 3
         )
        THEN 1 ELSE 0
    END
) AS DetailsDone,

        -- Child gatavie
        SUM(
            CASE 
                WHEN bp.ProductToPart_ID IS NOT NULL
                 AND CASE 
    WHEN EXISTS (
        SELECT 1
        FROM tasks t
        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1
    )
    AND NOT EXISTS (
        SELECT 1
        FROM tasks t
        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1
          AND t.Tasks_Status <> 3
    )
    THEN 1 ELSE 0
END
                THEN 1 ELSE 0
            END
        ) AS DetailsChildDone

    FROM batches_products bp
    WHERE bp.IsActive = 1
    GROUP BY bp.Batch_Id, bp.Version_Id
) dstat
ON dstat.Batch_Id = bp.Batch_Id
AND dstat.Version_Id = bp.Version_Id

WHERE bp.IsActive = 1
  AND b.IsActive = 1
  AND b.Batches_Statuss = 1

GROUP BY
    bp.Batch_Id,
    bp.Version_Id

ORDER BY b.ID DESC;
";

var list = new List<object>();

await using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync())
{
    list.Add(new
    {
        BatchId        = r.GetInt32(0),
        BatchCode      = r.GetString(1),
        BatchProductId = r.GetInt32(3), // MIN(bp.ID)
        VersionId      = r.GetInt32(4),
        ProductName    = r.GetString(5),
        ProductCode    = r.GetString(6),
        CategoryName   = r.GetString(7),
        VersionName    = r.GetString(8),
        IsPriority     = r.GetBoolean(9),
        
        Planned        = r.GetInt32(11),
        Comment        = r.IsDBNull(12) ? null : r.GetString(12),
        Sold   = r.GetInt32(13),
        Done   = r.GetInt32(14),
        DetailsTotal = r.GetInt32(15),
        DetailsChildTotal = r.GetInt32(16),
        DetailsDone = r.GetInt32(17),
        DetailsChildDone = r.GetInt32(18),
        DetailStart  = r.IsDBNull(19) ? (DateTime?)null : r.GetDateTime(19),
        DetailFinish = r.IsDBNull(20) ? (DateTime?)null : r.GetDateTime(20),
    });
}

return Ok(list);
}

[HttpGet("detail-qty")]
public async Task<IActionResult> GetDetailQty([FromQuery] int batchProductId)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    ptp.ID AS ProductToPartId,
    tp.TopPart_Name AS TopPartName,
    (
        SELECT bp2.ID
        FROM batches_products bp2
        WHERE bp2.Batch_Id = bp.Batch_Id
        AND bp2.Version_Id = bp.Version_Id
        AND bp2.ProductToPart_ID = ptp.ID
        AND bp2.IsActive = 1
        LIMIT 1
    ) AS BatchProductId,

    -- Parent = batch planned
    (
    SELECT bp2.Planned_Qty
FROM batches_products bp2
WHERE bp2.Batch_Id = (
    SELECT Batch_Id FROM batches_products WHERE ID = @bpId LIMIT 1
)
AND bp2.Version_Id = (
    SELECT Version_Id FROM batches_products WHERE ID = @bpId LIMIT 1
)
AND bp2.ProductToPart_ID IS NULL
LIMIT 1
) AS ParentQty,

    -- Child = sum of child rows
    SUM(
    CASE 
        WHEN bp.ProductToPart_ID = ptp.ID 
        THEN bp.Planned_Qty 
        ELSE 0 
    END
) AS ChildQty

FROM batches_products bp

JOIN producttopparts ptp 
    ON ptp.Version_ID = bp.Version_Id
    AND ptp.IsActive = 1

JOIN (
    SELECT DISTINCT ts.ProductToPart_ID
    FROM toppartsteps ts
    JOIN stage_step_type_map m
        ON m.Step_Type_ID = ts.Step_Type
        AND m.Stage = 1
        AND m.IsActive = 1
    WHERE ts.IsActive = 1
) ts ON ts.ProductToPart_ID = ptp.ID

JOIN toppart tp 
    ON tp.ID = ptp.TopPart_ID

WHERE bp.Batch_Id = (
    SELECT Batch_Id FROM batches_products WHERE ID = @bpId LIMIT 1
)

GROUP BY ptp.ID, tp.TopPart_Name;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            ProductToPartId = r.GetInt32(0),
            TopPartName     = r.GetString(1),
            BatchProductId = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            ParentQty = r.IsDBNull(3) ? 0 : r.GetInt32(3),
            ChildQty        = r.GetInt32(4)
        });
    }

    return Ok(list);
}

    } // <-- beidzas public class BatchesController

    // === DTO (tie paši nosaukumi, ko izmanto Blazor) ===
    public sealed class BatchCartModel
    {
        public int? BatchId { get; set; }   // create = null, update = >0
        public string Title { get; set; } = "";
        public string? Comment { get; set; }
        public List<BatchCartItem> Items { get; set; } = new();
    }
public sealed class BatchCartItem
{
    public int ProductId { get; set; }
    public int VersionId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int Qty { get; set; }
    public int? ProductToPartId { get; set; }
    public string? Comment { get; set; }   //  JAUNS
    public List<int> SelectedTopPartIds { get; set; } = new();
    public int? ItemId { get; set; }
}

public sealed class UpdateBatchProductDto
{
    public int BatchProductId { get; set; }
    public int PlannedQty { get; set; }
    public string? Comment { get; set; }
}

public sealed class SetPlannedDto
{
    public int BatchId { get; set; }
    public string Code { get; set; } = "";
}

public sealed class UpdateBatchCommentDto
{
    public int BatchId { get; set; }
    public string? Comment { get; set; }
}


} // <-- beidzas namespace ManiApi.Controllers
