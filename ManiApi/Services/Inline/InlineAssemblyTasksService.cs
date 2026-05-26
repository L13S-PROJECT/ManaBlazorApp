using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using ManiApi.Data;

namespace ManiApi.Services.Inline
{
    public class InlineAssemblyTasksService
    {
        private readonly AppDbContext _db;

        public InlineAssemblyTasksService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<int> GetAvailableAssemblyQty(int batchProductId)
            {
                var finishingQty = await _db.StockMovements
                    .Where(x =>
                        x.IsActive &&
                        x.BatchProduct_ID == batchProductId &&
                        x.Move_Type == MoveType.FINISHING)
                    .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

                var assemblyReserved = await _db.Tasks
                    .Join(
                        _db.TopPartSteps,
                        t => t.TopPartStep_ID,
                        ts => ts.Id,
                        (t, ts) => new { t, ts }
                    )
                    .Where(x =>
                        x.t.IsActive &&
                        x.t.BatchProduct_ID == batchProductId &&
                        x.ts.StepType == 2 &&
                        (x.t.Tasks_Status == 1 || x.t.Tasks_Status == 2))
                    .SumAsync(x => (int?)x.t.Qty_Done) ?? 0;

                return Math.Max(finishingQty - assemblyReserved, 0);
            }
    }
}