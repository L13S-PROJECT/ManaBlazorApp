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

    var reservedQty = await _db.Tasks
    .Where(x =>
        x.IsActive &&
        x.BatchProduct_ID == batchProductId &&
        x.Tasks_Status == 5)
    .SumAsync(x => (int?)x.Qty_Done) ?? 0;

    if (reservedQty > 0)
    return reservedQty;

    return Math.Max(assemblyQty, 0);
}

}