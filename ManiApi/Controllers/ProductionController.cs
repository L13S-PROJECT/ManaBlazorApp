using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
namespace ManiApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductionController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductionController(AppDbContext db) => _db = db;

    [HttpGet("overview-by-versions")]
    public async Task<IActionResult> GetOverview([FromQuery] List<int> versionIds)
    {
        if (versionIds == null || versionIds.Count == 0)
            return Ok(new List<object>());

        var data = await _db.StockMovements
            .Where(x => x.IsActive && versionIds.Contains(x.Version_ID))
            .GroupBy(x => new { x.Version_ID, x.Move_Type })
            .Select(g => new
            {
                VersionId = g.Key.Version_ID,
                MoveType = g.Key.Move_Type,
                Qty = g.Sum(x => x.Stock_Qty)
            })
            .ToListAsync();

        var result = versionIds.Select(id => new
        {
            VersionId = id,
            Assembly  = data.Where(x => x.VersionId == id && x.MoveType == MoveType.ASSEMBLY).Sum(x => x.Qty),
            Finishing = data.Where(x => x.VersionId == id && x.MoveType == MoveType.FINISHING).Sum(x => x.Qty),
            Stock     = data.Where(x => x.VersionId == id && x.MoveType == MoveType.STOCK).Sum(x => x.Qty)
        });

        return Ok(result);
    }
}