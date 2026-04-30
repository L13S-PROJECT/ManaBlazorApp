using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Stock
{
    public class StockService
    {
        private readonly AppDbContext _db;

        public StockService(AppDbContext db)
        {
            _db = db;
        }

        public async Task MoveAssemblyToFinishing(
            int batchProductId,
            int taskId,
            int qty,
            int? ralColorId)
        {
            var versionId = await _db.Set<BatchProduct>()
                .Where(x => x.ID == batchProductId)
                .Select(x => x.Version_Id)
                .FirstOrDefaultAsync();

            if (versionId == 0)
                throw new ArgumentException($"BatchProduct ar ID {batchProductId} nav atrasts.");

            _db.StockMovements.Add(
                StockMovementFactory.CreateAssemblyMovement(
                    versionId,
                    batchProductId,
                    taskId,
                    qty,
                    ralColorId));

            _db.StockMovements.Add(
                StockMovementFactory.CreateFinishingMovement(
                    versionId,
                    batchProductId,
                    taskId,
                    qty,
                    ralColorId));
        }

public async Task<int> CalculateAssemblyAvailable(int batchProductId)
{
    var assemblyStock = await _db.StockMovements
        .Where(x =>
            x.IsActive &&
            x.BatchProduct_ID == batchProductId &&
            x.Move_Type == MoveType.ASSEMBLY)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    var reservedForFinishing = await _db.Tasks
        .Join(_db.TopPartSteps,
            t => t.TopPartStep_ID,
            ts => ts.Id,
            (t, ts) => new { t, ts })
        .Where(x =>
            x.t.IsActive &&
            x.t.BatchProduct_ID == batchProductId &&
            x.ts.StepType == 3 &&
            x.t.Tasks_Status == 1 &&
            x.t.Qty_Done > 0)
        .SumAsync(x => (int?)x.t.Qty_Done) ?? 0;

    return Math.Max(assemblyStock - reservedForFinishing, 0);
}
    }
}