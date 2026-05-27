using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;
using ManiApi.Services.Tasks;

namespace ManiApi.Services.ProductionFlows.ParentInline;

public sealed class InlineParentFinishingService
{
    private readonly AppDbContext _db;
    private readonly TaskQueryService _queryService;
    public InlineParentFinishingService(
            AppDbContext db,
            TaskQueryService queryService)
        {
            _db = db;
            _queryService = queryService;
        }

    public async Task<int> GetAvailableDetailedQty(int batchProductId)
    {
        var detailedQty = await _db.StockMovements
            .Where(x =>
                x.BatchProduct_ID == batchProductId &&
                x.IsActive &&
                x.Move_Type == MoveType.DETAILED)
            .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

        var finishingQty = await _db.StockMovements
            .Where(x =>
                x.BatchProduct_ID == batchProductId &&
                x.IsActive &&
                x.Move_Type == MoveType.FINISHING)
            .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

        return Math.Max(detailedQty - finishingQty, 0);
    }

    public async Task OpenAssemblyWave(
    int finishingTaskId)
    {
        
        var task = await _db.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ID == finishingTaskId &&
                x.IsActive);

        if (task is null)
            throw new Exception("Finishing task not found.");

        var batchProductId = task.BatchProduct_ID;
        var qty = task.Qty_Done;
        var ralColorId = task.RAL_Color_ID;

        if (batchProductId <= 0)
            throw new Exception("BatchProduct_ID missing.");

        if (qty <= 0)
            throw new Exception("Wave qty invalid.");

        task.Qty_Done = qty;

        task.Tasks_Status = 3;
        task.Finished_At = DateTime.UtcNow;

        _db.Tasks.Update(task);

        // deaktivizējam tukšās waiting wave rindas
        var emptyWaitingTasks = await _db.Tasks
            .Where(x =>
                x.IsActive &&
                x.BatchProduct_ID == batchProductId &&
                x.Tasks_Status == 1 &&
                x.TopPartStep_ID == task.TopPartStep_ID &&
                x.Qty_Done <= 0)
            .ToListAsync();

        foreach (var row in emptyWaitingTasks)
        {
            row.IsActive = false;
        }

        await _db.SaveChangesAsync();

        var assemblyStepId = await _db.TopPartSteps
            .Where(x =>
                x.IsActive &&
                x.StepType == 2)
            .Join(
                _db.Tasks,
                ts => ts.Id,
                t => t.TopPartStep_ID,
                (ts, t) => new { ts, t })
            .Where(x =>
                x.t.ID == finishingTaskId)
            .Select(x => x.ts.Id)
            .FirstOrDefaultAsync();
        
        if (assemblyStepId <= 0)
            throw new Exception("Assembly step not found.");

        bool alreadyExists = await _db.Tasks
            .AnyAsync(x =>
                x.IsActive &&
                x.BatchProduct_ID == batchProductId &&
                x.TopPartStep_ID == assemblyStepId &&
                x.Tasks_Status != 3 &&
                x.RAL_Color_ID == ralColorId &&
                x.Qty_Done == qty);

        if (alreadyExists)
            {
                task.Tasks_Status = 3;
                task.Finished_At = DateTime.UtcNow;

                _db.Tasks.Update(task);

                await _db.SaveChangesAsync();

                return;
            }

        var assemblyTask = new ManiApi.Models.Tasks
            {
                BatchProduct_ID = batchProductId,
                TopPartStep_ID = assemblyStepId,

                Qty_Done = qty,
                RAL_Color_ID = ralColorId,

                Tasks_Status = 5,
                Finished_At = null,

                IsActive = true
            };

            _db.Tasks.Add(assemblyTask);

            await _db.SaveChangesAsync();

        var versionId = await _db.BatchProducts
                .Where(x => x.ID == batchProductId)
                .Select(x => x.Version_Id)
                .FirstAsync();

        await _db.SaveChangesAsync();

            _db.StockMovements.Add(new StockMovement
            {
                Version_ID = versionId,
                BatchProduct_ID = batchProductId,

                Move_Type = MoveType.FINISHING,
                Stock_Qty = -qty,

                RAL_Color_ID = ralColorId,

                Task_ID = finishingTaskId,
                IsActive = true
            });

            _db.StockMovements.Add(new StockMovement
            {
                Version_ID = versionId,
                BatchProduct_ID = batchProductId,

                Move_Type = MoveType.ASSEMBLY,
                Stock_Qty = qty,

                RAL_Color_ID = ralColorId,

                Task_ID = assemblyTask.ID,
                IsActive = true
            });

            await _db.SaveChangesAsync();

    }

}