using ManiApi.Data;
using ManiApi.Models;
using ManiApi.DTOs.Assembly;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Assembly;

public sealed class AssemblyTasksService
{
    private readonly AppDbContext _db;

    public AssemblyTasksService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AssemblySummaryDto>> GetAssemblySummary(int batchProductId)
        {
            var batchProduct = await _db.Set<BatchProduct>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == batchProductId && x.IsActive);

            if (batchProduct == null || batchProduct.Version_Id <= 0)
                return new List<AssemblySummaryDto>();

            var bp = batchProduct;

            var relatedBatchProducts = await _db.Set<BatchProduct>()
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Batch_Id == bp.Batch_Id &&
                    x.Version_Id == bp.Version_Id)
                .ToListAsync();

            var relatedBatchProductIds = relatedBatchProducts
                .Select(x => x.ID)
                .ToList();

            var isParent = bp.ProductToPart_ID == null;

            var hasChild = relatedBatchProducts
                .Any(x => x.ProductToPart_ID != null);

            var scenario =
                isParent && !hasChild ? "A" :
                isParent && hasChild  ? "B" :
                                        "C";
            
            var parts = await _db.Set<ProductTopPart>()
                    .AsNoTracking()
                    .Where(x =>
                        x.VersionId == bp.Version_Id &&
                        x.IsActive &&
                        (
                            scenario == "A" || scenario == "B"
                                ? true
                                : x.Id == bp.ProductToPart_ID
                        ))
                    .Select(x => new AssemblySummaryDto
                    {
                        ProductToPartId = x.Id,

                        TopPartName = _db.TopParts
                            .Where(tp => tp.Id == x.TopPartId)
                            .Select(tp => tp.TopPartName)
                            .FirstOrDefault() ?? "",

                        Qty = 0,
                        QtyDisplay = "",
                        Indicator = "gray",
                        StatusText = "NotStarted"
                    })
                    .ToListAsync();

            var validPartIds = await _db.Set<TopPartStep>()
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.StepType == 1 &&
                        !x.IsPainting)
                    .GroupBy(x => x.ProductToPartId)
                    .Where(g => g.Any(x => x.IsFinal))
                    .Select(g => g.Key)
                    .ToListAsync();

                parts = parts
                    .Where(x => validPartIds.Contains(x.ProductToPartId))
                    .ToList();

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
                        !x.ts.IsPainting &&
                        relatedBatchProductIds.Contains(x.t.BatchProduct_ID))
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
                            : x.Cnt1 == x.Total ? "orange"
                            : x.Cnt3 == x.Total ? "green"
                            : "yellow"
                    );

            foreach (var part in parts)
                    {
                        part.Indicator = indicators.TryGetValue(part.ProductToPartId, out var state)
                            ? state
                            : "gray";

                        var parentQty = relatedBatchProducts
                            .Where(x => x.ProductToPart_ID == null)
                            .Sum(x => x.Planned_Qty);

                        var childQty = relatedBatchProducts
                            .Where(x => x.ProductToPart_ID == part.ProductToPartId)
                            .Sum(x => x.Planned_Qty);

                        if (scenario == "C")
                        {
                            part.Qty = childQty;
                            part.QtyDisplay = $"+{childQty}";
                        }
                        else if (childQty > 0)
                        {
                            part.Qty = parentQty + childQty;
                            part.QtyDisplay = $"{parentQty}+{childQty}";
                        }
                        else
                        {
                            part.Qty = parentQty;
                            part.QtyDisplay = $"{parentQty}";
                        }
                    }

            foreach (var part in parts)
                    {
                        part.StatusText =
                            part.Indicator == "gray"   ? "Nav iesākts" :
                            part.Indicator == "orange" ? "Gaida" :
                            part.Indicator == "yellow" ? "Procesā" :
                                                        "Gatavs";
                    }

                    return parts;

        }
}