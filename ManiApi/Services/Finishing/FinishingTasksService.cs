//FinishingTasksService.cs

using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;

namespace ManiApi.Services.Finishing
{
    public class FinishingTasksService
    {
        private readonly AppDbContext _db;

        public FinishingTasksService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<(bool isPainting, int availableQty)> GetChildFinishingData(
    int batchProductId,
    int productToPartId)
{
    // 1) pārbaudām IsPainting (Detailed solis!)
    var isPainting = await _db.TopPartSteps
        .AnyAsync(x =>
            x.IsActive &&
            x.ProductToPartId == productToPartId &&
            x.StepType == 1 &&
            x.IsPainting == true);

    // 2) vai visi FINAL soļi pirms painting ir pabeigti
var paintingStep = await _db.TopPartSteps
    .FirstOrDefaultAsync(x =>
        x.IsActive &&
        x.ProductToPartId == productToPartId &&
        x.StepType == 1 &&
        x.IsPainting == true);

if (paintingStep == null)
    return (false, 0);

if (!isPainting)
    return (false, 0);

var notFinished = await _db.Tasks
    .Join(_db.TopPartSteps,
        t => t.TopPartStep_ID,
        ts => ts.Id,
        (t, ts) => new { t, ts })
    .Where(x =>
        x.t.IsActive &&
        x.t.BatchProduct_ID == batchProductId &&
        x.ts.ProductToPartId == productToPartId &&
        x.ts.IsFinal &&
        x.ts.StepOrder < paintingStep.StepOrder)
    .AnyAsync(x => x.t.Tasks_Status != 3);

if (notFinished)
    return (false, 0);

    // 3) DETAIL stock (child finishing source)
        var detailStock = await _db.StockMovements
            .Where(x =>
                x.IsActive &&
                x.BatchProduct_ID == batchProductId &&
                x.Move_Type == MoveType.DETAILED)
            .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

// 4) Reserved for painting (Finishing tasks with status=1 or 2)

    var reservedForPainting = await _db.Tasks
    .Join(_db.TopPartSteps,
        t => t.TopPartStep_ID,
        ts => ts.Id,
        (t, ts) => new { t, ts })
    .Where(x =>
        x.t.IsActive &&
        x.t.BatchProduct_ID == batchProductId &&
        x.ts.ProductToPartId == productToPartId &&
        x.ts.StepType == 1 &&
        x.ts.IsPainting == true &&
        (x.t.Tasks_Status == 1 || x.t.Tasks_Status == 2))
    .SumAsync(x => (int?)x.t.Qty_Done) ?? 0;

var available = Math.Max(detailStock - reservedForPainting, 0);

    return (isPainting && available > 0, available);
}

    }

}
