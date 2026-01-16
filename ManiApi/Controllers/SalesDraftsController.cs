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
                    SELECT
                        Version_Id,
                        BatchProduct_Id,
                        BatchCode,
                        Qty,
                        IsAssembly
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
                                VersionId       = r.GetInt32(0),
                                BatchProductId  = r.GetInt32(1),
                                BatchCode       = r.GetString(2),
                                Qty             = r.GetInt32(3),
                                IsAssembly      = r.GetBoolean(4)
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
    (SalesDraft_Id, Version_Id, BatchProduct_Id, BatchCode, IsAssembly, Qty)
VALUES
    (@did, @vid, @bpid, @bcode, @isAsm, @qty)
ON DUPLICATE KEY UPDATE
    Qty = VALUES(Qty);
";

cmd.Parameters.Add(new MySqlParameter("@did", draftId));
cmd.Parameters.Add(new MySqlParameter("@vid", it.VersionId));
cmd.Parameters.Add(new MySqlParameter("@bpid", it.BatchProductId));
cmd.Parameters.Add(new MySqlParameter("@bcode", it.BatchCode));
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
        var draftItems = new List<(int VersionId, int BatchProductId, bool IsAssembly, int Qty)>();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
SELECT Version_Id, BatchProduct_Id, IsAssembly, Qty
FROM sales_draft_items
WHERE SalesDraft_Id = @did;
";
            cmd.Parameters.Add(new MySqlParameter("@did", draftId));

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                draftItems.Add((
                    r.GetInt32(0), // VersionId
                    r.GetInt32(1), // BatchProductId
                    r.GetBoolean(2),
                    r.GetInt32(3)
                ));

            }
        }

        // 3️⃣ apstrādājam katru rindu
        foreach (var item in draftItems)
        {
    
            var moveType = item.IsAssembly ? "ASSEMBLY" : "STOCK";

await using var ins = conn.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = @"
INSERT INTO stock_movements
(Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, IsActive)
VALUES
(@vid, @bp, @type, @qty, 1);
";

            ins.Parameters.Add(new MySqlParameter("@vid", item.VersionId));
            ins.Parameters.Add(new MySqlParameter("@bp",  item.BatchProductId));
            ins.Parameters.Add(new MySqlParameter("@type", moveType));
            ins.Parameters.Add(new MySqlParameter("@qty", -item.Qty));


await ins.ExecuteNonQueryAsync();
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
    public int BatchProductId { get; set; }
    public string BatchCode { get; set; } = "";
    public int Qty { get; set; }
    public bool IsAssembly { get; set; }
}


}
