using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Detail
{
    public class DetailTasksService
    {
        private readonly AppDbContext _db;

        public DetailTasksService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DetailTasksDto> GetDetailTasks(int batchProductId)
        {
            var batchProduct = await _db.Set<BatchProduct>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == batchProductId && x.IsActive);
            var bp = batchProduct;


        var relatedBatchProductIds = await _db.Set<BatchProduct>()
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.Batch_Id == bp!.Batch_Id &&
                x.Version_Id == bp.Version_Id)
            .Select(x => x.ID)
            .ToListAsync();

            // nosakām scenāriju
                var isParent = bp!.ProductToPart_ID == null;

                var hasChild = await _db.Set<BatchProduct>()
                    .AnyAsync(x =>
                        x.IsActive &&
                        x.Batch_Id == bp.Batch_Id &&
                        x.Version_Id == bp.Version_Id &&
                        x.ProductToPart_ID != null);

                var scenario =
                    isParent && !hasChild ? "A" :
                    isParent && hasChild  ? "B" :
                                            "C";

                var relatedBatchProducts = await _db.Set<BatchProduct>()
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.Batch_Id == bp.Batch_Id &&
                        x.Version_Id == bp.Version_Id)
                    .ToListAsync();

                var childPartIds = relatedBatchProducts
                    .Where(x => x.ProductToPart_ID != null)
                    .Select(x => x.ProductToPart_ID!.Value)
                    .ToList();

            if (batchProduct == null || batchProduct.Version_Id <= 0)
                return new DetailTasksDto();

            var parts = await _db.Set<ProductTopPart>()
                .AsNoTracking()
                .Where(x =>
                        x.VersionId == batchProduct.Version_Id &&
                        x.IsActive &&
                        (
                            scenario == "A" || scenario == "B"
                                ? true
                                : childPartIds.Contains(x.Id)
                        )
                    )
                .Select(x => new DetailPartDto
                        {
                            ProductToPartId = x.Id,
                            TopPartName = _db.TopParts
                                .Where(tp => tp.Id == x.TopPartId)
                                .Select(tp => tp.TopPartName)
                                .FirstOrDefault(),

                            Qty = 0,
                            QtyDisplay = "",
                            Indicator = "gray",
                            IsActivated = false,
                            StartDate = null,
                            EndDate = null,
                            Steps = new List<DetailStepDto>()
                        })
                .ToListAsync();

            var stepRows = await _db.Set<TopPartStep>()
    .AsNoTracking()
    .Where(x => x.IsActive && parts.Select(p => p.ProductToPartId).Contains(x.ProductToPartId) && x.StepType == 1)
    .OrderBy(x => x.ProductToPartId)
    .ThenBy(x => x.StepOrder)
    .Select(x => new
    {
        x.Id,
        x.ProductToPartId,
        x.StepName
    })
    .ToListAsync();

var activePartIds = await _db.Tasks
    .AsNoTracking()
    .Join(_db.TopPartSteps,
        t => t.TopPartStep_ID,
        ts => ts.Id,
        (t, ts) => new { t, ts })
    .Where(x =>
            x.t.IsActive &&
            x.ts.IsActive &&
            x.t.Tasks_Status != 5 &&
            x.ts.StepType == 1 &&
            relatedBatchProductIds.Contains(x.t.BatchProduct_ID)
        )
    .Select(x => x.ts.ProductToPartId)
    .Distinct()
    .ToListAsync();

var indicatorRows = await _db.Tasks
    .AsNoTracking()
    .Join(_db.TopPartSteps,
        t => t.TopPartStep_ID,
        ts => ts.Id,
        (t, ts) => new { t, ts })
    .Where(x =>
            x.t.IsActive &&
            x.ts.IsActive &&
            x.ts.StepType == 1 &&
            relatedBatchProductIds.Contains(x.t.BatchProduct_ID)
        )
    .GroupBy(x => x.ts.ProductToPartId)
    .Select(g => new
    {
        ProductToPartId = g.Key,
        Cnt1 = g.Count(x => x.t.Tasks_Status == 1),
        Cnt2 = g.Count(x => x.t.Tasks_Status == 2),
        Cnt3 = g.Count(x => x.t.Tasks_Status == 3),
        Cnt5 = g.Count(x => x.t.Tasks_Status == 5),
        Total = g.Count()
    })
    .ToListAsync();

var indicators = indicatorRows.ToDictionary(
    x => x.ProductToPartId,
    x =>
        x.Cnt5 == x.Total ? "gray"
        : x.Cnt1 == x.Total ? "blue"
        : x.Cnt3 == x.Total ? "green"
        : "yellow"
);

var taskRows = await _db.Tasks
    .AsNoTracking()
    .Join(_db.TopPartSteps,
        t => t.TopPartStep_ID,
        ts => ts.Id,
        (t, ts) => new { t, ts })
    .Where(x =>
            x.t.IsActive &&
            x.ts.IsActive &&
            x.ts.StepType == 1 &&
            relatedBatchProductIds.Contains(x.t.BatchProduct_ID)
        )
    .Select(x => new
    {
        TaskId = x.t.ID,
        x.ts.Id,
        x.ts.ProductToPartId,
        Status = x.t.Tasks_Status,
        AssignedTo = x.t.Assigned_To,
        ClaimedBy = x.t.Claimed_By,
        StartedAt = x.t.Started_At,
        FinishedAt = x.t.Finished_At,
        Comment = x.t.Tasks_Comment,
        IsCommentForEmployee = x.t.Is_Comment_For_Employee
    })
    .ToListAsync();



foreach (var part in parts)
{
    part.Indicator = indicators.TryGetValue(part.ProductToPartId, out var state)
        ? state
        : "gray";

    part.IsActivated = activePartIds.Contains(part.ProductToPartId);

        var parentQty = relatedBatchProducts
            .Where(x => x.ProductToPart_ID == null)
            .Sum(x => x.Planned_Qty);

        var childQty = relatedBatchProducts
            .Where(x => x.ProductToPart_ID == part.ProductToPartId)
            .Sum(x => x.Planned_Qty);

        // tikai display (UI izmantos)
        if (scenario == "C")
            {
                part.Qty = childQty;
                part.QtyDisplay = $"+{childQty}";
            }
            else if (childQty > 0)
            {
                part.Qty = parentQty;
                part.QtyDisplay = $"{parentQty}+{childQty}";
            }
            else
            {
                part.Qty = parentQty;
                part.QtyDisplay = $"{parentQty}";
            }

    part.Steps = stepRows
    .Where(x => x.ProductToPartId == part.ProductToPartId)
    .Select(x =>
    {
        var tasks = taskRows
            .Where(t =>
                t.Id == x.Id &&
                t.ProductToPartId == part.ProductToPartId)
            .ToList();

return new DetailStepDto
{
    TaskId = tasks.Select(t => t.TaskId).FirstOrDefault(),
    StepId = x.Id,
    StepName = x.StepName,

    AssignedTo = tasks.Select(t => t.AssignedTo).Distinct().Count() == 1
        ? tasks.First().AssignedTo
        : null,

    ClaimedBy = tasks.Select(t => t.ClaimedBy).Distinct().Count() == 1
        ? tasks.First().ClaimedBy
        : null,

    StartedAt = tasks.Where(t => t.StartedAt.HasValue).Any()
        ? tasks.Where(t => t.StartedAt.HasValue).Min(t => t.StartedAt)
        : null,

    FinishedAt = tasks.All(t => t.FinishedAt.HasValue)
        ? tasks.Max(t => t.FinishedAt)
        : null,

    Comment = tasks.Select(t => t.Comment).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),

    IsCommentForEmployee = tasks.Any(t => t.IsCommentForEmployee),

    Status = tasks.Any(t => t.Status == 2) ? 2
        : tasks.All(t => t.Status == 3) ? 3
        : tasks.All(t => t.Status == 1) ? 1
        : 5
};
    })
    .ToList();
}

parts = parts
    .Where(p => p.Steps != null && p.Steps.Any())
    .ToList();

            return new DetailTasksDto
            {
                Parts = parts
            };
        }
    }
}