using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using ManiApi.Data;

namespace ManiApi.Services.ProductionFlows.ParentSeparate;

public sealed class ParentSeparateFinishingService
{
    private readonly AppDbContext _db;

    public ParentSeparateFinishingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetAvailableAssemblyQty(int batchProductId)
{
    var assemblyQty = await _db.StockMovements
        .Where(x =>
            x.BatchProduct_ID == batchProductId &&
            x.IsActive &&
            x.Move_Type == MoveType.ASSEMBLY)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;
    
    var finishingQty = await _db.StockMovements
        .Where(x =>
            x.BatchProduct_ID == batchProductId &&
            x.IsActive &&
            x.Move_Type == MoveType.FINISHING)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    var remainderQty = await _db.Tasks
            .Join(_db.TopPartSteps,
                t => t.TopPartStep_ID,
                ts => ts.Id,
                (t, ts) => new { t, ts })

            .Where(x =>
                x.t.IsActive &&
                x.t.BatchProduct_ID == batchProductId &&
                x.t.Tasks_Status == 5 &&
                x.ts.StepType == 3)

            .SumAsync(x => (int?)x.t.Qty_Done) ?? 0;

        return remainderQty > 0
            ? remainderQty
            : assemblyQty;
}

}