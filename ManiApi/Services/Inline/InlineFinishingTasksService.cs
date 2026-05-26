using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using ManiApi.Data;

namespace ManiApi.Services.Inline
{
    public class InlineFinishingTasksService
    {
        private readonly AppDbContext _db;

        public InlineFinishingTasksService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<bool> IsInlinePainting(int batchProductId)
                {
                    return await _db.BatchProducts
                        .Where(bp => bp.ID == batchProductId)
                        .Join(
                            _db.ProductVersions,
                            bp => bp.Version_Id,
                            v => v.Id,
                            (bp, v) => v.ProductionModel
                        )
                        .AnyAsync(x => x == 1);
                }
    public async Task<bool> IsDetailFinished(int batchProductId)
            {
                var notFinished = await _db.Tasks
                    .Join(
                        _db.TopPartSteps,
                        t => t.TopPartStep_ID,
                        ts => ts.Id,
                        (t, ts) => new { t, ts }
                    )
                    .Where(x =>
                        x.t.IsActive &&
                        x.t.BatchProduct_ID == batchProductId &&
                        x.ts.StepType == 1 &&
                        !x.ts.IsPainting &&
                        x.t.Tasks_Status != 3)
                    .AnyAsync();

                return !notFinished;
            }


    public async Task<int> GetAvailableInlineQty(int batchProductId)
            {
                var detailedQty = await _db.StockMovements
                    .Where(x =>
                        x.IsActive &&
                        x.BatchProduct_ID == batchProductId &&
                        x.Move_Type == MoveType.DETAILED)
                    .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

                var reservedQty = await _db.Tasks
                    .Join(
                        _db.TopPartSteps,
                        t => t.TopPartStep_ID,
                        ts => ts.Id,
                        (t, ts) => new { t, ts }
                    )
                    .Where(x =>
                        x.t.IsActive &&
                        x.t.BatchProduct_ID == batchProductId &&
                        x.ts.StepType == 3 &&
                        (x.t.Tasks_Status == 1 || x.t.Tasks_Status == 2))
                    .SumAsync(x => (int?)x.t.Qty_Done) ?? 0;

                return Math.Max(detailedQty - reservedQty, 0);
            }

    }
}