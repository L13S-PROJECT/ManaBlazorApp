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

        public async Task MoveToFinishing(
            int batchProductId,
            int taskId,
            int qty,
            int? ralColorId)
        {
           Console.WriteLine(
                    $"MOVE TO FINISHING -> bp={batchProductId} qty={qty} ral={ralColorId}"
                );

            var versionId = await _db.Set<BatchProduct>()
                .Where(x => x.ID == batchProductId)
                .Select(x => x.Version_Id)
                .FirstOrDefaultAsync();

            var productionModel = await _db.Set<BatchProduct>()
                .Where(x => x.ID == batchProductId)
                .Join(
                    _db.ProductVersions,
                    bp => bp.Version_Id,
                    v => v.Id,
                    (bp, v) => v.ProductionModel
                )
                .FirstOrDefaultAsync();

            var productToPartId = await _db.Set<BatchProduct>()
                .Where(x => x.ID == batchProductId)
                .Select(x => x.ProductToPart_ID)
                .FirstOrDefaultAsync();

            var isInlinePainting = productionModel == 1;
            var isChild = productToPartId != null;

            Console.WriteLine(
                $"MOVE FLOW -> inline={isInlinePainting} child={isChild}"
            );

            if (versionId == 0)
                throw new ArgumentException($"BatchProduct ar ID {batchProductId} nav atrasts.");

            var sourceMoveType =
                isChild || isInlinePainting
                    ? "DETAILED"
                    : "ASSEMBLY";

            Console.WriteLine(
                $"MOVE SOURCE -> {sourceMoveType} -> FINISHING"
            );

            Console.WriteLine("ADDING SOURCE MOVEMENT");

            _db.StockMovements.Add(


                sourceMoveType == "ASSEMBLY"

                    ? StockMovementFactory.CreateMovement(
                        versionId,
                        batchProductId,
                        taskId,
                        MoveType.ASSEMBLY,
                        -qty,
                        ralColorId)

                    : StockMovementFactory.CreateMovement(
                        versionId,
                        batchProductId,
                        taskId,
                        MoveType.DETAILED,
                        -qty,
                        ralColorId)

            );

            Console.WriteLine("ADDING FINISHING MOVEMENT");

            _db.StockMovements.Add(
                StockMovementFactory.CreateFinishingMovement(
                    versionId,
                    batchProductId,
                    taskId,
                    qty,
                    ralColorId));

    Console.WriteLine("MOVE TO FINISHING DONE");

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