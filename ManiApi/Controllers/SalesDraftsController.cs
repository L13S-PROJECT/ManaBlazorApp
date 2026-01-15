using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/sales-drafts")]
    public class SalesDraftsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public SalesDraftsController(AppDbContext db) => _db = db;

        // ==============================
        // GET: /api/sales-drafts/last
        // ==============================
        [HttpGet("last")]
        public async Task<IActionResult> GetLastDraft()
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            int? draftId = null;

            // 1️⃣ atrodam aktīvo draftu
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT ID
FROM sales_drafts
WHERE Is_Committed = 0
ORDER BY ID DESC
LIMIT 1;
";
                var obj = await cmd.ExecuteScalarAsync();
                if (obj != null && obj != DBNull.Value)
                    draftId = Convert.ToInt32(obj);
            }

            if (!draftId.HasValue)
                return NoContent();

            // 2️⃣ ielādējam rindas
            var items = new List<object>();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT Version_Id, Qty, IsAssembly
FROM sales_draft_items
WHERE SalesDraft_Id = @did; 
";
                var p = cmd.CreateParameter();
                p.ParameterName = "@did";
                p.Value = draftId.Value;
                cmd.Parameters.Add(p);

                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    items.Add(new
                {
                    VersionId = r.GetInt32(0),
                    Qty = r.GetInt32(1),
                    IsAssembly = r.GetBoolean(2)
                });

                }
            }

            return Ok(new
            {
                DraftId = draftId.Value,
                Items = items
            });
        }

        // ==============================
        // POST: /api/sales-drafts/autosave
        // ==============================
        [HttpPost("autosave")]
        public async Task<IActionResult> AutoSave([FromBody] SalesDraftDto dto)
        {
            if (dto == null || dto.Items == null)
                return BadRequest();

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            int draftId;

            // 1️⃣ atrodam vai izveidojam draftu
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
SELECT ID
FROM sales_drafts
WHERE Is_Committed = 0
ORDER BY ID DESC
LIMIT 1;
";
                var obj = await cmd.ExecuteScalarAsync();
                if (obj != null && obj != DBNull.Value)
                {
                    draftId = Convert.ToInt32(obj);
                }
                else
                {
                    cmd.CommandText = @"
INSERT INTO sales_drafts (Is_Committed)
VALUES (0);
SELECT LAST_INSERT_ID();
";
                    draftId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }

 
            // 3️⃣ ieliekam jaunas rindas
foreach (var it in dto.Items)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;
cmd.CommandText = @"
INSERT INTO sales_draft_items
    (SalesDraft_Id, Version_Id, IsAssembly, Qty)
VALUES
    (@did, @vid, @isAsm, @qty)
ON DUPLICATE KEY UPDATE
    Qty = VALUES(Qty);
";

cmd.Parameters.Add(new MySqlParameter("@did", draftId));
cmd.Parameters.Add(new MySqlParameter("@vid", it.VersionId));
cmd.Parameters.Add(new MySqlParameter("@isAsm", it.IsAssembly ? 1 : 0));
cmd.Parameters.Add(new MySqlParameter("@qty", it.Qty));

    await cmd.ExecuteNonQueryAsync();
}


            await tx.CommitAsync();

            return Ok(new { DraftId = draftId });
        }

        // ==============================
        // POST: /api/sales-drafts/commit
        // ==============================
        [HttpPost("commit")]
public async Task<IActionResult> Commit()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    try
    {
        int draftId;

        // 1️⃣ atrodam aktīvo draftu
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
SELECT ID
FROM sales_drafts
WHERE Is_Committed = 0
ORDER BY ID DESC
LIMIT 1;";
            var obj = await cmd.ExecuteScalarAsync();
            if (obj == null || obj == DBNull.Value)
                return NotFound("Nav aktīva melnraksta.");

            draftId = Convert.ToInt32(obj);
        }

        // 2️⃣ nolasa drafta rindas
        var draftItems = new List<(int VersionId, bool IsAssembly, int Qty)>();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
SELECT Version_Id, IsAssembly, Qty
FROM sales_draft_items
WHERE SalesDraft_Id = @did;";
            cmd.Parameters.Add(new MySqlParameter("@did", draftId));

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                draftItems.Add((
                    r.GetInt32(0),
                    r.GetBoolean(1),
                    r.GetInt32(2)
                ));
            }
        }

        // 3️⃣ apstrādājam katru rindu
        foreach (var item in draftItems)
        {
            var remaining = item.Qty;
            var moveType = item.IsAssembly ? "ASSEMBLY" : "STOCK";

            // atrodam batchproductus ar atlikumu (FIFO)
            var batches = new List<(int BatchProductId, int AvailableQty)>();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
SELECT
    bp.ID,
    (bp.Planned_Qty - COALESCE(SUM(sm.Stock_Qty),0)) AS AvailableQty
FROM batches_products bp
JOIN batches b ON b.ID = bp.Batch_Id
LEFT JOIN stock_movements sm 
       ON sm.BatchProduct_ID = bp.ID
       AND sm.IsActive = 1
WHERE
    bp.Version_Id = @vid
    AND bp.IsActive = 1
    AND b.IsActive = 1
GROUP BY bp.ID, bp.Planned_Qty
HAVING AvailableQty > 0
ORDER BY bp.ID ASC;";


                cmd.Parameters.Add(new MySqlParameter("@vid", item.VersionId));
                cmd.Parameters.Add(new MySqlParameter("@type", moveType));

                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    batches.Add((
                        r.GetInt32(0),
                        r.GetInt32(1)
                    ));
                }
            }

            foreach (var b in batches)
            {
                if (remaining <= 0)
                    break;

                var take = Math.Min(b.AvailableQty, remaining);

                await using var ins = conn.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO stock_movements
(BatchProduct_ID, Move_Type, Stock_Qty, IsActive)
VALUES
(@bp, @type, @qty, 1);";

                ins.Parameters.Add(new MySqlParameter("@bp", b.BatchProductId));
                ins.Parameters.Add(new MySqlParameter("@type", moveType));
                ins.Parameters.Add(new MySqlParameter("@qty", -take));

                await ins.ExecuteNonQueryAsync();

                remaining -= take;
            }

            if (remaining > 0)
                throw new Exception($"Nepietiek atlikuma VersionId={item.VersionId} ({moveType})");
        }

        // 4️⃣ atzīmējam draftu kā committed
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
UPDATE sales_drafts
SET Is_Committed = 1
WHERE ID = @id;";
            cmd.Parameters.Add(new MySqlParameter("@id", draftId));
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return Ok(new { DraftId = draftId, Committed = true });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return BadRequest(ex.Message);
    }
}


        // ==============================
        // POST: /api/sales-drafts/clear
        // ==============================
        [HttpPost("clear")]
        public async Task<IActionResult> ClearDraft()
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
UPDATE sales_drafts
SET Is_Committed = 1
WHERE Is_Committed = 0;
";
            await cmd.ExecuteNonQueryAsync();

            return Ok();
        }
    }

    // ==============================
    // DTO
    // ==============================
    public sealed class SalesDraftDto
    {
        public List<SalesDraftItemDto> Items { get; set; } = new();
    }

public sealed class SalesDraftItemDto
{
    public int VersionId { get; set; }
    public int Qty { get; set; }
    public bool IsAssembly { get; set; }   // ← JAUNS
}

}
