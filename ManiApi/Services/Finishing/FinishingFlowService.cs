using ManiApi.Data;
using ManiApi.DTOs.Tasks;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;
using ManiApi.Services.Tasks;
using ManiApi.Services.Stock;

namespace ManiApi.Services.Finishing
{
    public class FinishingFlowService
    {
        private readonly AppDbContext _db;
        private readonly StockService _stockService;
        private readonly FinishingTasksService _finishingTasksService;
        public FinishingFlowService(
                AppDbContext db,
                StockService stockService,
                FinishingTasksService finishingTasksService)
        {
            _db = db;
            _stockService = stockService;
            _finishingTasksService = finishingTasksService;
        }

public async Task<OpenFinishingResultDto> OpenFinishing(OpenFinishingDto dto)
        {
            try
                {
// 1) INPUT VALIDATION -> Ievades datu pārbaude – pārliecināmies, ka visi obligātie parametri ir derīgi
Console.WriteLine(
    $"INLINE TEST -> bp={dto.BatchProductId} part={dto.ProductToPartId} qty={dto.Qty}");

                    ValidateInput(dto);

// 2) BEGIN TRANSACTION -> Sākam datubāzes transakciju, lai visas darbības izpildītos kā viens vesels

                    await using var tx = await _db.Database.BeginTransactionAsync();

// 3) GET FINISHING STEP - Atrodam konkrētajai detaļai definēto Finishing soli (StepType = 3)
                    TopPartStep finishingStep;

                        if (dto.ProductToPartId > 0)
                        {
                            finishingStep = await GetFinishingStep(dto.ProductToPartId);
                        }
                        else
                        {
                            finishingStep = await _db.TopPartSteps
                                .FirstAsync(ts =>
                                    ts.StepType == 3 &&
                                    ts.IsActive);
                        }

                    var batchProductId = dto.BatchProductId;

// 4) CALCULATE ASSEMBLY AVAILABLE QTY ->
//Aprēķinām pieejamo Assembly daudzumu, atņemot jau rezervēto Finishing apjomu

      int availableQty;

            if (dto.ProductToPartId > 0)
            {
                var childData = await _finishingTasksService
                    .GetChildFinishingData(
                        batchProductId,
                        dto.ProductToPartId);

                availableQty = childData.availableQty;
            }
            else
            {
                availableQty = await _db.StockMovements
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

                availableQty = Math.Max(availableQty - finishingQty, 0);
            }

// 5) GET WAITING FINISHING TASKS (status=5) ->
// Atlasām visus gaidošos (status=5) Finishing uzdevumus šai partijai un detaļai

                    var waitingTasks = await GetWaitingTasks(
                            batchProductId,
                            finishingStep.Id,
                            dto.ProductToPartId);

// 6) CREATE OR SPLIT FINISHING WAVE -> 
// Nosakām – vai veidojam jaunu Finishing vilni vai izmantojam/sadalām esošo

                    var activeTask = await CreateOrSplitFinishingTask(
                        waitingTasks,
                        batchProductId,
                        finishingStep.Id,
                        availableQty,
                        dto);

        // 7) CREATE STOCK MOVEMENTS (ASSEMBLY -> FINISHING) -> 
        // Veidojam stock kustības – pārvietojam daudzumu no Assembly uz Finishing
Console.WriteLine("CALLING MOVE TO FINISHING");

                await _db.SaveChangesAsync();
                await _stockService.MoveToFinishing(
                        batchProductId,
                        activeTask.ID,
                        dto.Qty,
                        dto.RalColorId);
Console.WriteLine("MOVE TO FINISHING RETURNED");
        // 8) COMMIT TRANSACTION - Apstiprinām transakciju – visas izmaiņas saglabājam datubāzē
                await _db.SaveChangesAsync();
    
    Console.WriteLine("INLINE SAVE OK");

                await tx.CommitAsync();

        // 9) RETURN RESULT - Atgriežam rezultātu ar jaunizveidotā/atjauninātā uzdevuma ID

                    return new OpenFinishingResultDto
                    {
                        TaskId = activeTask.ID
                    };

                    }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR open-finishing: " + ex.ToString());
                    throw;
                }
        }
// Palīgmetodes ievades datu validācijai (var izsaukt arī no kontroliera, ja nepieciešams) -
// OpeningFinishing metodes sākumā var aizvietot tieši ar šo, lai kods būtu tīrāks un validācija centralizēta

private void ValidateInput(OpenFinishingDto dto)
    {
        if (dto.BatchProductId <= 0 || dto.Qty <= 0)
            throw new ArgumentException("BatchProductId, ProductToPartId un Qty ir obligāti, Qty > 0.");
    }

private async Task<TopPartStep> GetFinishingStep(int productToPartId)
{
    var finishingStep = await _db.TopPartSteps
        .FirstOrDefaultAsync(ts =>
            ts.ProductToPartId == productToPartId &&
            ts.StepType == 3 &&
            ts.IsActive);

    if (finishingStep is null)
        throw new ArgumentException("Šai detaļai nav definēts Finishing solis (StepType = 3).");

    return finishingStep;
}

private async Task<List<ManiApi.Models.Tasks>> GetWaitingTasks(
    int batchProductId,
    int finishingStepId,
    int? sourceProductToPartId)
{
    return await _db.Tasks
        .Where(t =>
            t.IsActive &&
            t.BatchProduct_ID == batchProductId &&
            t.TopPartStep_ID == finishingStepId &&
            t.Tasks_Status == 5 &&
            t.Source_ProductToPart_ID == sourceProductToPartId)
        .OrderBy(t => t.ID)
        .ToListAsync();
}

private async Task<ManiApi.Models.Tasks> CreateOrSplitFinishingTask(
    List<ManiApi.Models.Tasks> waitingTasks,
    int batchProductId,
    int finishingStepId,
    int assemblyAvailable,
    OpenFinishingDto dto)
{
    ManiApi.Models.Tasks activeTask;

 var existingWave = await _db.Tasks
    .FirstOrDefaultAsync(x =>
        x.IsActive &&
        x.BatchProduct_ID == batchProductId &&
        x.TopPartStep_ID == finishingStepId &&
        x.RAL_Color_ID == dto.RalColorId &&
        x.Tasks_Status == 1 &&
        x.Qty_Done > 0);

    bool isMerge = existingWave != null;
    
    if (existingWave != null)
        {
            var zeroQtyTasks = await _db.Tasks
                .Where(x =>
                    x.IsActive &&
                    x.BatchProduct_ID == batchProductId &&
                    x.Tasks_Status == 1 &&
                    x.Qty_Done <= 0)
                .ToListAsync();

            foreach (var row in zeroQtyTasks)
            {
                row.IsActive = false;
            }
            
            existingWave.Qty_Done += dto.Qty;

            var remainingToTake = dto.Qty;

                foreach (var waiting in waitingTasks.OrderBy(x => x.ID))
                {
                    if (remainingToTake <= 0)
                        break;

                    var takeQty = Math.Min(waiting.Qty_Done, remainingToTake);

                    waiting.Qty_Done -= takeQty;

                    if (waiting.Qty_Done <= 0)
                    {
                        waiting.Qty_Done = 0;
                        waiting.IsActive = false;
                    }

                    remainingToTake -= takeQty;
                }

            return existingWave;
        }

    if (waitingTasks.Count == 0)
    {
        var placeholderTask = await _db.Tasks
            .FirstOrDefaultAsync(x =>
                x.IsActive &&
                x.BatchProduct_ID == batchProductId &&
                x.TopPartStep_ID == finishingStepId &&
                x.Tasks_Status == 1 &&
                x.Qty_Done <= 0);

        var remaining = Math.Max(assemblyAvailable - dto.Qty, 0);

        activeTask = ManiApi.Services.Tasks.TaskFactory.CreateFinishingTask(
            batchProductId,
            finishingStepId,
            dto.Qty,
            dto.RalColorId,
            dto.Comment);

        activeTask.Source_ProductToPart_ID = dto.ProductToPartId;

        _db.Tasks.Add(activeTask);
        // SaveChanges tiks izsaukts augstāk (OpenFinishing)


        if (placeholderTask != null)
            {
                placeholderTask.IsActive = false;
            }

        if (remaining > 0)
            {
                var waitingRemainder = new ManiApi.Models.Tasks
                {
                    BatchProduct_ID = batchProductId,
                    TopPartStep_ID = finishingStepId,
                    Source_ProductToPart_ID = dto.ProductToPartId,
                    Tasks_Status = 5,
                    IsActive = true,
                    Qty_Done = remaining,
                    Qty_Scrap = 0
                };

                _db.Tasks.Add(waitingRemainder);
            }
    }
    else
    {
        var parent = waitingTasks[0];
        var plannedQty = waitingTasks.Sum(x => x.Qty_Done);
        var requestQty = dto.Qty;

        if (plannedQty <= 0 || requestQty >= plannedQty)
        {
            var zeroQtyTasks = await _db.Tasks
                .Where(x =>
                    x.IsActive &&
                    x.BatchProduct_ID == batchProductId &&
                    x.Tasks_Status == 1 &&
                    x.Qty_Done <= 0)
                .ToListAsync();

            foreach (var row in zeroQtyTasks)
            {
                row.IsActive = false;
            }
            
            parent.Tasks_Status  = 1;
            parent.Qty_Done      = requestQty > 0 ? requestQty : plannedQty;
            parent.Tasks_Comment = dto.Comment;
            parent.RAL_Color_ID  = dto.RalColorId;
            parent.Source_ProductToPart_ID = dto.ProductToPartId;

            foreach (var extra in waitingTasks.Skip(1))
                extra.IsActive = false;

            // SaveChanges tiks izsaukts augstāk (OpenFinishing)

            activeTask = parent;
        }
        else
        {
            var remaining = plannedQty - requestQty;

            parent.IsActive = false;

            activeTask = ManiApi.Services.Tasks.TaskFactory.CreateFinishingTask(
                batchProductId,
                finishingStepId,
                dto.Qty,
                dto.RalColorId,
                dto.Comment);

            activeTask.Source_ProductToPart_ID = dto.ProductToPartId;

            _db.Tasks.Add(activeTask);

            var waitingRemainder = new ManiApi.Models.Tasks
            {
                BatchProduct_ID = parent.BatchProduct_ID,
                TopPartStep_ID  = parent.TopPartStep_ID,
                Source_ProductToPart_ID = dto.ProductToPartId,
                Tasks_Status    = 5,
                IsActive        = true,
                Qty_Done        = remaining,
                Qty_Scrap       = 0
            };

            _db.Tasks.Add(waitingRemainder);

            foreach (var extra in waitingTasks.Skip(1))
                extra.IsActive = false;

            // SaveChanges tiks izsaukts augstāk (OpenFinishing)
        }
    }

    return activeTask;
}

    }
}