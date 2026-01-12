using Microsoft.AspNetCore.Mvc;
using ManiApi.Models;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SalesController(AppDbContext db)
    {
        _db = db;
    }

    // POST: /api/sales/commit
    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] SalesCommitDto dto)
    {
        if (dto == null || dto.Items.Count == 0)
            return BadRequest("Nav nevienas pārdošanas rindas.");

        // 🔒 ŠEIT BŪS transakcija
        var conn = _db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            // Pārdošana: stock pārbaude + OUT kustība
            // 1) pārbaudīt stock (SELECT SUM stock_movements)
            // 2) ja nepietiek → throw
            // 3) INSERT stock_movements (Move_Type = 'SALE', Qty = -X)
            // 4) saglabāt dokumentu
var results = new List<object>();

            foreach (var item in dto.Items)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT COALESCE(SUM(Stock_Qty), 0)
FROM stock_movements
WHERE Version_Id = @vid
  AND Move_Type = 'STOCK'
  AND IsActive = 1;
";

    var pVid = cmd.CreateParameter();
    pVid.ParameterName = "@vid";
    pVid.Value = item.VersionId;
    cmd.Parameters.Add(pVid);

    var obj = await cmd.ExecuteScalarAsync();
    var inStock = Convert.ToInt32(obj);

    if (item.Qty > inStock)
    {
        throw new InvalidOperationException(
            $"Nepietiek stock versijai {item.VersionId}. Pieejams: {inStock}, pieprasīts: {item.Qty}"
        );

    }

    await using var ins = conn.CreateCommand();
ins.Transaction = tx;

ins.CommandText = @"
INSERT INTO stock_movements
    (Version_Id, Move_Type, Stock_Qty, Created_At, IsActive)
VALUES
    (@vid, 'OUT', @qty, NOW(), 1);
";

var pVid2 = ins.CreateParameter();
pVid2.ParameterName = "@vid";
pVid2.Value = item.VersionId;
ins.Parameters.Add(pVid2);

var pQty = ins.CreateParameter();
pQty.ParameterName = "@qty";
pQty.Value = -item.Qty;   // ⬅️ KRITISKI: mīnus
ins.Parameters.Add(pQty);

await ins.ExecuteNonQueryAsync();

await using var sumCmd = conn.CreateCommand();
sumCmd.Transaction = tx;

sumCmd.CommandText = @"
SELECT COALESCE(SUM(Stock_Qty), 0)
FROM stock_movements
WHERE Version_Id = @vid
  AND Move_Type = 'STOCK'
  AND IsActive = 1;
";

var pVidSum = sumCmd.CreateParameter();
pVidSum.ParameterName = "@vid";
pVidSum.Value = item.VersionId;
sumCmd.Parameters.Add(pVidSum);

var newInStock = Convert.ToInt32(await sumCmd.ExecuteScalarAsync());

results.Add(new
{
    versionId = item.VersionId,
    inStock = newInStock
});

}

           await tx.CommitAsync();

                    return Ok(new
                    {
                        ok = true,
                        items = results
                    });

        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }
}
