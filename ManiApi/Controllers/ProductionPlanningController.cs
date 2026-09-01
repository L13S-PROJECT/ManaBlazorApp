using ManaApp.Shared.DTOs.Planning;
using ManiApi.Data;
using ManiApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Services.Planning;
using ManiApi.Services.Tasks;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/production-planning")]
    public class ProductionPlanningController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly PlanningPartRequirementService _partRequirementService;
        private readonly TaskNewDependencyService _taskDependencyService;
        private readonly ProductionRequirementService _productionRequirementService;

        public ProductionPlanningController(
            AppDbContext db,
            PlanningPartRequirementService partRequirementService,
            TaskNewDependencyService taskDependencyService,
            ProductionRequirementService productionRequirementService)
        {
            _db = db;
            _partRequirementService = partRequirementService;
            _taskDependencyService = taskDependencyService;
            _productionRequirementService = productionRequirementService;
        }

        [HttpPost("draft/items")]
        public async Task<IActionResult> SaveDraftItem(
            [FromBody] SavePlanningDraftItemRequest dto)
        {
            if (dto.PlannedQty <= 0)
                return BadRequest("Plānotajam daudzumam jābūt lielākam par 0.");

            var topPart = await _db.TopParts
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.TopPartId &&
                    x.IsActive);

            if (topPart == null)
                return NotFound("Prece vai rezerves daļa nav atrasta.");

            var workflow = await _db.Workflows
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.WorkflowId &&
                    x.Status == WorkflowStatus.Released &&
                    x.IsActive);

            if (workflow == null)
                return BadRequest("Izvēlētais Workflow nav derīgs.");

            var workflowIsValid = topPart.TopPartType switch
            {
                TopPartType.Product =>
                    workflow.TopPartId == (uint)dto.TopPartId,

                TopPartType.SparePart =>
                    workflow.TopPartId.HasValue &&
                    await _db.TopPartSpareParts.AnyAsync(link =>
                        link.SparePartTopPartId == (uint)dto.TopPartId &&
                        link.ProductTopPartId == workflow.TopPartId.Value &&
                        link.WorkflowId == workflow.Id &&
                        link.IsActive),

                _ => false
            };

            if (!workflowIsValid)
                return BadRequest("Izvēlētais Workflow nav derīgs šim TopPart.");

            var draft = await _db.ProductionPlanningDrafts
                .FirstOrDefaultAsync(x =>
                    x.IsActive &&
                    x.Status == ProductionPlanningDraftStatus.Draft);

            if (draft == null)
            {
                draft = new ProductionPlanningDraft
                    {
                        Created_At = DateTime.UtcNow,
                        Status = ProductionPlanningDraftStatus.Draft,
                        IsActive = true
                    };

                _db.ProductionPlanningDrafts.Add(draft);
                    await _db.SaveChangesAsync();
            }

            var item = await _db.ProductionPlanningDraftItems
                .FirstOrDefaultAsync(x =>
                    x.Draft_ID == draft.ID &&
                    x.TopPart_ID == dto.TopPartId &&
                    x.Workflow_ID == dto.WorkflowId);

            if (item == null)
            {
                item = new ProductionPlanningDraftItem
                {
                    Draft_ID = draft.ID,
                    TopPart_ID = dto.TopPartId,
                    Workflow_ID = dto.WorkflowId,
                    Planned_Qty = dto.PlannedQty,
                    Created_At = DateTime.UtcNow,
                    IsActive = true
                };

                _db.ProductionPlanningDraftItems.Add(item);
            }
            else
            {
                item.Planned_Qty = dto.PlannedQty;
                item.IsActive = true;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                item.ID,
                item.Draft_ID,
                item.TopPart_ID,
                item.Workflow_ID,
                item.Planned_Qty
            });
        }

        [HttpGet("parts")]
        public async Task<ActionResult<List<PlanningPartListItemDto>>> GetParts()
        {
            Dictionary<int, int> plannedQuantities;

            try
            {
                plannedQuantities =
                    await _partRequirementService
                        .CalculateDraftPartQuantitiesAsync();
            }
            catch (InvalidOperationException exception)
            {
                return UnprocessableEntity(exception.Message);
            }

            var partIds = plannedQuantities
                .Where(row => row.Value > 0)
                .Select(row => row.Key)
                .ToList();

            var rows = await _db.TopParts
                .AsNoTracking()
                .Where(topPart =>
                    topPart.IsActive &&
                    topPart.TopPartType == TopPartType.Part &&
                    partIds.Contains(topPart.Id))
                .OrderBy(topPart => topPart.TopPartName)
                .Select(topPart => new PlanningPartListItemDto
                {
                    TopPartId = topPart.Id,
                    PartName = topPart.TopPartName,
                    PartCode = topPart.TopPartCode
                })
                .ToListAsync();

            foreach (var row in rows)
                row.PlanQty = plannedQuantities[row.TopPartId];

            return Ok(rows);
        }

        [HttpGet("products")]
        public async Task<ActionResult<List<PlanningProductListItemDto>>> GetProducts()
        {
            var rows = await _db.TopParts
                .AsNoTracking()
                .Where(topPart =>
                    topPart.IsActive &&
                    topPart.TopPartType == TopPartType.Product)
                .OrderBy(topPart => topPart.TopPartName)
                .Select(topPart => new PlanningProductListItemDto
                {
                    TopPartId = topPart.Id,
                    ProductName = topPart.TopPartName,
                    ProductCode = topPart.TopPartCode,
                    CategoryId = topPart.CategoryID,
                    CategoryName = _db.Categories
                        .Where(category => category.Id == topPart.CategoryID)
                        .Select(category => category.CategoryName)
                        .FirstOrDefault() ?? "",
                    ParentCategoryName = _db.Categories
                        .Where(category => category.Id == topPart.CategoryID)
                        .Select(category => category.ParentId == null
                            ? category.CategoryName
                            : _db.Categories
                                .Where(parent => parent.Id == category.ParentId)
                                .Select(parent => parent.CategoryName)
                                .FirstOrDefault() ?? "")
                        .FirstOrDefault() ?? "",
                    TopPartCategoryId = (int?)topPart.TopPartCategoryID,

                    PlanQty = _db.ProductionPlanningDraftItems
                        .Where(item =>
                                item.TopPart_ID == topPart.Id &&
                                item.IsActive &&
                                item.Draft.IsActive &&
                                item.Draft.Status == ProductionPlanningDraftStatus.Draft)
                        .Sum(item => (int?)item.Planned_Qty) ?? 0,

                    WaitingQty = _db.ProductionBatchTopParts
                        .Where(item =>
                            item.TopPart_ID == topPart.Id &&
                            item.IsActive &&
                            item.Batch!.IsActive)
                        .Sum(item => (int?)item.Planned_Qty) ?? 0
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("products/{topPartId:int}/workflows")]
        public async Task<ActionResult<List<PlanningWorkflowOptionDto>>> GetWorkflows(
            int topPartId)
        {
            var rows = await _db.Workflows
                .AsNoTracking()
                .Where(x =>
                    x.TopPartId == (uint)topPartId &&
                    x.Status == WorkflowStatus.Released &&
                    x.IsActive)
                .OrderByDescending(x => x.IsCurrent)
                .ThenByDescending(x => x.WorkflowVersion)
                .Select(x => new PlanningWorkflowOptionDto
                {
                    WorkflowId = x.Id,
                    WorkflowVersion = x.WorkflowVersion,
                    Name = x.Name ?? "",
                    IsCurrent = x.IsCurrent
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("correction/batches")]
        public async Task<ActionResult<List<PlanningCorrectionBatchOptionDto>>>
            GetCorrectionBatches()
        {
            var rows = await _db.ProductionBatches
                .AsNoTracking()
                .Where(batch =>
                    batch.IsActive &&
                    _db.ProductionBatchTopParts.Any(item =>
                        item.Batch_ID == batch.ID &&
                        item.IsActive &&
                        item.Done_Qty < item.Planned_Qty))
                .OrderByDescending(batch => batch.ID)
                .Select(batch => new PlanningCorrectionBatchOptionDto
                {
                    BatchId = batch.ID,
                    BatchCode = batch.Batch_Code,
                    StartDate = batch.Start_Date,

                    PlannedQty = _db.ProductionBatchTopParts
                        .Where(item =>
                            item.Batch_ID == batch.ID &&
                            item.IsActive)
                        .Sum(item => (int?)item.Planned_Qty) ?? 0,

                    DoneQty = _db.ProductionBatchTopParts
                        .Where(item =>
                            item.Batch_ID == batch.ID &&
                            item.IsActive)
                        .Sum(item => (int?)item.Done_Qty) ?? 0
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("correction/batches/{batchId:int}")]
        public async Task<ActionResult<PlanningCorrectionBatchDto>>
            GetCorrectionBatch(int batchId)
        {
            var batch = await _db.ProductionBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ID == (uint)batchId &&
                    x.IsActive);

            if (batch == null)
                return NotFound("Ražošanas partija nav atrasta.");

            var items = await _db.ProductionBatchTopParts
                .AsNoTracking()
                .Where(x =>
                    x.Batch_ID == batch.ID &&
                    x.IsActive)
                .OrderBy(x => x.ID)
                .Select(x => new PlanningCorrectionBatchItemDto
                {
                    BatchTopPartId = x.ID,
                    TopPartId = x.TopPart_ID,
                    ProductName = x.TopPart!.TopPartName,
                    ProductCode = x.TopPart.TopPartCode,
                    TopPartType = (byte)x.TopPart.TopPartType,
                    IsWorkflowCurrent = x.Workflow!.IsCurrent,
                    ParentCategoryName =
                        x.TopPart.TopPartType == TopPartType.SparePart
                            ? _db.TopParts
                                .Where(product =>
                                    x.Workflow.TopPartId.HasValue &&
                                    product.Id == (int)x.Workflow.TopPartId.Value)
                                .Select(product => _db.Categories
                                    .Where(category => category.Id == product.CategoryID)
                                    .Select(category => category.ParentId == null
                                        ? category.CategoryName
                                        : _db.Categories
                                            .Where(parent => parent.Id == category.ParentId)
                                            .Select(parent => parent.CategoryName)
                                            .FirstOrDefault() ?? "")
                                    .FirstOrDefault() ?? "")
                                .FirstOrDefault() ?? ""
                            : _db.Categories
                                .Where(category => category.Id == x.TopPart.CategoryID)
                                .Select(category => category.ParentId == null
                                    ? category.CategoryName
                                    : _db.Categories
                                        .Where(parent => parent.Id == category.ParentId)
                                        .Select(parent => parent.CategoryName)
                                        .FirstOrDefault() ?? "")
                                .FirstOrDefault() ?? "",
                    WorkflowId = x.Workflow_ID,
                    WorkflowVersion = x.Workflow!.WorkflowVersion,
                    PlannedQty = (int)x.Planned_Qty,
                    DoneQty = (int)x.Done_Qty,

                    // Pagaidu nosacījums līdz Tasks pieslēgšanai.
                    CanEdit = true
                })
                .ToListAsync();

            return Ok(new PlanningCorrectionBatchDto
            {
                BatchId = batch.ID,
                BatchCode = batch.Batch_Code,
                StartDate = batch.Start_Date,
                Items = items
            });
        }

        [HttpGet("draft/items")]
        public async Task<ActionResult<List<PlanningDraftItemDto>>> GetDraftItems()
        {
            var rows = await (
                from item in _db.ProductionPlanningDraftItems.AsNoTracking()
                join draft in _db.ProductionPlanningDrafts.AsNoTracking()
                    on item.Draft_ID equals draft.ID
                join topPart in _db.TopParts.AsNoTracking()
                    on item.TopPart_ID equals topPart.Id
                join workflow in _db.Workflows.AsNoTracking()
                    on item.Workflow_ID equals workflow.Id
                join category in _db.Categories.AsNoTracking()
                    on topPart.CategoryID equals (int?)category.Id
                    into categoryGroup
                from category in categoryGroup.DefaultIfEmpty()
                join parentCategory in _db.Categories.AsNoTracking()
                    on category.ParentId equals (int?)parentCategory.Id
                    into parentCategoryGroup
                from parentCategory in parentCategoryGroup.DefaultIfEmpty()

                join workflowProduct in _db.TopParts.AsNoTracking()
                    on workflow.TopPartId equals (uint?)workflowProduct.Id
                    into workflowProductGroup
                from workflowProduct in workflowProductGroup.DefaultIfEmpty()

                join workflowCategory in _db.Categories.AsNoTracking()
                    on workflowProduct.CategoryID equals (int?)workflowCategory.Id
                    into workflowCategoryGroup
                from workflowCategory in workflowCategoryGroup.DefaultIfEmpty()

                join workflowParentCategory in _db.Categories.AsNoTracking()
                    on workflowCategory.ParentId equals (int?)workflowParentCategory.Id
                    into workflowParentCategoryGroup
                from workflowParentCategory in workflowParentCategoryGroup.DefaultIfEmpty()

                where item.IsActive
                    && draft.IsActive
                    && draft.Status == ProductionPlanningDraftStatus.Draft
                orderby item.ID
                select new PlanningDraftItemDto
                {
                    DraftItemId = item.ID,
                    TopPartId = (int)item.TopPart_ID,
                    TopPartType = (byte)topPart.TopPartType,
                    ProductName = topPart.TopPartName,
                    ProductCode = topPart.TopPartCode,
                    WorkflowId = item.Workflow_ID,
                    WorkflowVersion = workflow.WorkflowVersion,
                    IsWorkflowCurrent = workflow.IsCurrent,
                    PlannedQty = item.Planned_Qty,
                    ParentCategoryId =
                        topPart.TopPartType == TopPartType.SparePart
                            ? workflowParentCategory == null
                                ? null
                                : workflowParentCategory.Id
                            : parentCategory == null
                                ? null
                                : parentCategory.Id,

                    ParentCategoryName =
                        topPart.TopPartType == TopPartType.SparePart
                            ? workflowParentCategory == null
                                ? ""
                                : workflowParentCategory.CategoryName
                            : parentCategory == null
                                ? ""
                                : parentCategory.CategoryName
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost("draft/save")]
        public async Task<IActionResult> SaveDraft(
            [FromBody] SavePlanningDraftRequest dto)
            {
                var batchCode = dto.BatchCode?.Trim();

                if (string.IsNullOrWhiteSpace(batchCode))
                    return BadRequest("Batch-Nr. ir obligāts.");

                if (batchCode.Length > 50)
                    return BadRequest("Batch-Nr. nedrīkst pārsniegt 50 rakstzīmes.");

                if (await _db.ProductionBatches.AnyAsync(x =>
                        x.Batch_Code == batchCode))
                    return Conflict("Šāds Batch-Nr. jau eksistē.");

                var draft = await _db.ProductionPlanningDrafts
                    .FirstOrDefaultAsync(x =>
                        x.IsActive &&
                        x.Status == ProductionPlanningDraftStatus.Draft);

                if (draft == null)
                    return NotFound("Aktīvs melnraksts nav atrasts.");

                var draftItems = await _db.ProductionPlanningDraftItems
                    .Where(x =>
                        x.Draft_ID == draft.ID &&
                        x.IsActive)
                    .ToListAsync();

                if (draftItems.Count == 0)
                    return BadRequest("Melnrakstā nav nevienas preces.");

                if (draftItems.Any(x => x.Planned_Qty <= 0))
                    return BadRequest("Plānotajam daudzumam jābūt lielākam par 0.");

                var today = DateTime.Today;

                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable);

                var batch = new ProductionBatch
                {
                    Batch_Code = batchCode,
                    Start_Date = today,
                    Created_At = DateTime.UtcNow,
                    IsActive = true
                };

                _db.ProductionBatches.Add(batch);
                await _db.SaveChangesAsync();

                var batchItems = draftItems.Select(x =>
                    new ProductionBatchTopPart
                    {
                        Batch_ID = batch.ID,
                        TopPart_ID = x.TopPart_ID,
                        Workflow_ID = x.Workflow_ID,
                        Planned_Qty = (uint)x.Planned_Qty,
                        Done_Qty = 0,
                        IsPriority = false,
                        IsActive = true
                   }).ToList();

                _db.ProductionBatchTopParts.AddRange(batchItems);
                await _db.SaveChangesAsync();

                var executions = batchItems
                    .Select(batchItem => new ProductionExecution
                    {
                        ProductionBatchTopPart_ID = batchItem.ID,
                        ProductionRequirement_ID = null,
                        TopPart_ID = batchItem.TopPart_ID,
                        Workflow_ID = batchItem.Workflow_ID,
                        Quantity = (int)batchItem.Planned_Qty,
                        Status = ProductionExecutionStatus.WAITING,
                        Created_At = DateTime.UtcNow,
                        IsActive = true
                    })
                    .ToList();

                _db.ProductionExecutions.AddRange(executions);

                await _db.SaveChangesAsync();

                var incomingExecutions =
                    await _productionRequirementService
                        .CreateForExecutionsAsync(executions);

                executions.AddRange(incomingExecutions);

                var workflowIds = executions
                    .Select(x => x.Workflow_ID)
                    .Distinct()
                    .ToList();

                var processNodes = await _db.WorkflowNodes
                    .AsNoTracking()
                    .Where(x =>
                        workflowIds.Contains(x.WorkflowId) &&
                        x.NodeType == (byte)WorkflowNodeType.Process &&
                        x.IsActive)
                    .ToListAsync();

                var workflowWithoutProcess = workflowIds
                    .FirstOrDefault(workflowId =>
                        !processNodes.Any(node =>
                            node.WorkflowId == workflowId));

                if (workflowWithoutProcess != 0)
                    return BadRequest(
                        $"Workflow ID {workflowWithoutProcess} nav neviena aktīva PROCESS mezgla.");

                var processWithoutWorkCenter = processNodes
                    .FirstOrDefault(x => !x.WorkCenterId.HasValue);

                if (processWithoutWorkCenter != null)
                    return BadRequest(
                        $"PROCESS mezglam ID {processWithoutWorkCenter.Id} nav norādīts darba centrs.");
                
                var processNodeIds = processNodes
                    .Select(x => x.Id)
                    .ToList();

                var processComponents = await (
                    from processComponent in _db.WorkflowProcessComponents.AsNoTracking()

                    join workflowComponent in _db.WorkflowComponents.AsNoTracking()
                        on processComponent.WorkflowComponentId equals workflowComponent.Id

                    where processNodeIds.Contains(processComponent.ProcessNodeId)
                        && workflowComponent.ComponentType == 1
                        && workflowComponent.IsActive

                    select processComponent
                ).ToListAsync();

                var workflowIdByProcessNodeId = processNodes
                    .ToDictionary(
                        x => x.Id,
                        x => x.WorkflowId);

                var componentStagings = executions
                    .SelectMany(execution => processComponents
                        .Where(processComponent =>
                            workflowIdByProcessNodeId[processComponent.ProcessNodeId]
                                == execution.Workflow_ID)
                        .Select(processComponent => new ProductionComponentStaging
                        {
                            ProductionExecution_ID = execution.ID,
                            WorkflowProcessComponent_ID = processComponent.Id,
                            RequiredQuantity =
                                processComponent.Quantity * execution.Quantity,
                            StagedQuantity = 0,
                            StagedByEmployee_ID = null,
                            Staged_At = null,
                            IsActive = true
                        }))
                    .ToList();

                _db.ProductionComponentStagings.AddRange(componentStagings);

                await _db.SaveChangesAsync();

                var taskCreatedAt = DateTime.UtcNow;

                var tasks = executions
                    .SelectMany(execution => processNodes
                        .Where(node =>
                            node.WorkflowId == execution.Workflow_ID)
                        .Select(node => new TaskNew
                        {
                            ProductionExecution_ID = execution.ID,
                            WorkflowNode_ID = node.Id,
                            Employee_ID = null,
                            WorkCenter_ID = node.WorkCenterId!.Value,
                            Quantity = execution.Quantity,
                            Status = TaskNewStatus.WAITING,
                            Created_At = taskCreatedAt,
                            IsActive = true
                        }))
                    .ToList();

                _db.TasksNew.AddRange(tasks);

                await _db.SaveChangesAsync();

                foreach (var execution in executions)
                    {
                        await _taskDependencyService.CreateForExecutionAsync(
                            execution.ID,
                            execution.Workflow_ID);
                    }

                await _db.SaveChangesAsync();

                var taskHistories = tasks

                    .Select(task => new TaskNewStatusHistory
                    {
                        TaskNew_ID = task.ID,
                        FromStatus = null,
                        ToStatus = TaskNewStatus.WAITING,
                        Changed_At = taskCreatedAt,
                        Comment = "Task automātiski izveidots, apstiprinot ražošanas plānu."
                    })
                    .ToList();

                _db.TaskNewStatusHistories.AddRange(taskHistories);

                await _db.SaveChangesAsync();

                draft.Batch_Code = batchCode;
                draft.Plan_Date = DateOnly.FromDateTime(today);
                draft.Status = ProductionPlanningDraftStatus.Saved;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    batch.ID,
                    batch.Batch_Code,
                    batch.Start_Date
                });
            }

    [HttpPut("draft/items/{draftItemId:int}")]
        public async Task<IActionResult> UpdateDraftItem(
            int draftItemId,
            [FromBody] UpdatePlanningDraftItemRequest dto)
        {
            if (dto.PlannedQty <= 0)
                return BadRequest("Plānotajam daudzumam jābūt lielākam par 0.");

            var item = await _db.ProductionPlanningDraftItems
                .FirstOrDefaultAsync(x =>
                    x.ID == (uint)draftItemId &&
                    x.IsActive &&
                    x.Draft.IsActive &&
                    x.Draft.Status == ProductionPlanningDraftStatus.Draft);

            if (item == null)
                return NotFound("Melnraksta rinda nav atrasta.");

            var topPart = await _db.TopParts
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == item.TopPart_ID &&
                    x.IsActive);

            var workflow = await _db.Workflows
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.WorkflowId &&
                    x.Status == WorkflowStatus.Released &&
                    x.IsActive);

            if (topPart == null || workflow == null)
                return BadRequest("Izvēlētais Workflow nav derīgs.");

            var workflowIsValid = topPart.TopPartType switch
            {
                TopPartType.Product =>
                    workflow.TopPartId == (uint)item.TopPart_ID,

                TopPartType.SparePart =>
                    workflow.TopPartId.HasValue &&
                    await _db.TopPartSpareParts.AnyAsync(link =>
                        link.SparePartTopPartId == (uint)item.TopPart_ID &&
                        link.ProductTopPartId == workflow.TopPartId.Value &&
                        link.WorkflowId == workflow.Id &&
                        link.IsActive),

                _ => false
            };

            if (!workflowIsValid)
                return BadRequest("Izvēlētais Workflow nav derīgs šim TopPart.");

            var duplicateExists =
                await _db.ProductionPlanningDraftItems.AnyAsync(x =>
                    x.ID != item.ID &&
                    x.Draft_ID == item.Draft_ID &&
                    x.TopPart_ID == item.TopPart_ID &&
                    x.Workflow_ID == dto.WorkflowId &&
                    x.IsActive);

            if (duplicateExists)
                return Conflict("Šī prece ar izvēlēto Workflow jau ir melnrakstā.");

            item.Workflow_ID = dto.WorkflowId;
            item.Planned_Qty = dto.PlannedQty;

            await _db.SaveChangesAsync();

            return NoContent();
        }

    [HttpDelete("draft/items/{draftItemId:int}")]
        public async Task<IActionResult> DeleteDraftItem(int draftItemId)
        {
            var item = await _db.ProductionPlanningDraftItems
                .FirstOrDefaultAsync(x =>
                    x.ID == (uint)draftItemId &&
                    x.IsActive &&
                    x.Draft.IsActive &&
                    x.Draft.Status == ProductionPlanningDraftStatus.Draft);

            if (item == null)
                return NotFound("Melnraksta rinda nav atrasta.");

            _db.ProductionPlanningDraftItems.Remove(item);

            await _db.SaveChangesAsync();

            return NoContent();
        }

    [HttpDelete("draft")]
public async Task<IActionResult> DeleteDraft()
{
    var draft = await _db.ProductionPlanningDrafts
        .FirstOrDefaultAsync(x =>
            x.IsActive &&
            x.Status == ProductionPlanningDraftStatus.Draft);

    if (draft == null)
        return NotFound("Aktīvs melnraksts nav atrasts.");

    var draftItems = await _db.ProductionPlanningDraftItems
        .Where(x =>
            x.Draft_ID == draft.ID &&
            x.IsActive)
        .ToListAsync();

    foreach (var item in draftItems)
        item.IsActive = false;

    draft.IsActive = false;

    await _db.SaveChangesAsync();

    return NoContent();
}

        [HttpDelete("correction/batches/{batchId:int}")]
        public async Task<IActionResult> DeleteCorrectionBatch(int batchId)
        {
            var batch = await _db.ProductionBatches
                .FirstOrDefaultAsync(x =>
                    x.ID == (uint)batchId &&
                    x.IsActive);

            if (batch == null)
                return NotFound("Ražošanas partija nav atrasta.");

            var items = await _db.ProductionBatchTopParts
                .Where(x =>
                    x.Batch_ID == batch.ID &&
                    x.IsActive)
                .ToListAsync();

            batch.IsActive = false;

            foreach (var item in items)
                item.IsActive = false;

            await _db.SaveChangesAsync();

            return NoContent();
        }

    private static string? ValidateCorrectionRequest(
    UpdatePlanningCorrectionBatchRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BatchCode))
                return "Batch-Nr. ir obligāts.";

            if (dto.BatchCode.Trim().Length > 50)
                return "Batch-Nr. nedrīkst pārsniegt 50 rakstzīmes.";

            if (dto.Items.Count == 0)
                return "Ražošanas plānā jābūt vismaz vienai rindai.";

            if (dto.Items.Any(x => x.PlannedQty <= 0))
                return "Plānotajam daudzumam jābūt lielākam par 0.";

            if (dto.Items
                .GroupBy(x => new { x.TopPartId, x.WorkflowId })
                .Any(group => group.Count() > 1))
            {
                return "Ražošanas plānā ir dublētas rindas.";
            }

            if (dto.Items
                .Where(x => x.BatchTopPartId != 0)
                .GroupBy(x => x.BatchTopPartId)
                .Any(group => group.Count() > 1))
            {
                return "Ražošanas plānā ir dublēti rindu identifikatori.";
            }

            return null;
        }

    private async Task<string?> ValidateCorrectionItemsAsync(
            UpdatePlanningCorrectionBatchRequest dto)
        {
            var topPartIds = dto.Items
                .Select(x => x.TopPartId)
                .Distinct()
                .ToList();

            var workflowIds = dto.Items
                .Select(x => x.WorkflowId)
                .Distinct()
                .ToList();

            var topParts = await _db.TopParts
                .AsNoTracking()
                .Where(x =>
                    topPartIds.Contains(x.Id) &&
                    x.IsActive)
                .ToDictionaryAsync(x => x.Id);

            var workflows = await _db.Workflows
                .AsNoTracking()
                .Where(x =>
                    workflowIds.Contains(x.Id) &&
                    x.Status == WorkflowStatus.Released &&
                    x.IsActive)
                .ToDictionaryAsync(x => x.Id);

            var sparePartIds = topParts.Values
                .Where(x => x.TopPartType == TopPartType.SparePart)
                .Select(x => (uint)x.Id)
                .ToList();

            var sparePartLinks = await _db.TopPartSpareParts
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    sparePartIds.Contains(x.SparePartTopPartId) &&
                    workflowIds.Contains(x.WorkflowId))
                .ToListAsync();

            foreach (var item in dto.Items)
            {
                if (!topParts.TryGetValue(item.TopPartId, out var topPart) ||
                    !workflows.TryGetValue(item.WorkflowId, out var workflow))
                {
                    return "Prece vai Workflow nav derīgs.";
                }

                var isValid = topPart.TopPartType switch
                {
                    TopPartType.Product =>
                        workflow.TopPartId == (uint)item.TopPartId,

                    TopPartType.SparePart =>
                        workflow.TopPartId.HasValue &&
                        sparePartLinks.Any(link =>
                            link.SparePartTopPartId == (uint)item.TopPartId &&
                            link.ProductTopPartId == workflow.TopPartId.Value &&
                            link.WorkflowId == workflow.Id),

                    _ => false
                };

                if (!isValid)
                    return "Izvēlētais Workflow nav derīgs šim TopPart.";
            }

            return null;
        }

    
        [HttpPut("correction/batches/{batchId:int}")]
        public async Task<IActionResult> UpdateCorrectionBatch(
            int batchId,
            [FromBody] UpdatePlanningCorrectionBatchRequest dto)
        {
            if (batchId <= 0)
                return BadRequest("Ražošanas partijas ID nav derīgs.");

            var validationError = ValidateCorrectionRequest(dto);

            if (validationError is not null)
                return BadRequest(validationError);

            validationError = await ValidateCorrectionItemsAsync(dto);

            if (validationError is not null)
                return BadRequest(validationError);

            var batch = await _db.ProductionBatches
                .FirstOrDefaultAsync(x =>
                    x.ID == (uint)batchId &&
                    x.IsActive);

            if (batch == null)
                return NotFound("Ražošanas partija nav atrasta.");

            var batchCode = dto.BatchCode.Trim();

            if (await _db.ProductionBatches.AnyAsync(x =>
                    x.ID != batch.ID &&
                    x.Batch_Code == batchCode))
            {
                return Conflict("Šāds Batch-Nr. jau eksistē.");
            }

            var batchItems = await _db.ProductionBatchTopParts
                .Where(x => x.Batch_ID == batch.ID)
                .ToListAsync();

            var existingItems = batchItems
                .Where(x => x.IsActive)
                .ToList();

            var inactiveItems = batchItems
                .Where(x => !x.IsActive)
                .ToList();

            var existingById = existingItems
                .ToDictionary(x => x.ID);

            foreach (var requestItem in dto.Items
                .Where(x => x.BatchTopPartId != 0))
            {
                if (!existingById.TryGetValue(
                        requestItem.BatchTopPartId,
                        out var existingItem))
                {
                    return BadRequest(
                        "Korekcijas rinda nepieder izvēlētajai ražošanas partijai.");
                }

                if (existingItem.TopPart_ID != requestItem.TopPartId)
                {
                    return BadRequest(
                        "Esošai korekcijas rindai preci mainīt nedrīkst.");
                }
            }

            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            batch.Batch_Code = batchCode;

            foreach (var existingItem in existingItems)
            {
                var requestItem = dto.Items.FirstOrDefault(x =>
                    x.BatchTopPartId == existingItem.ID);

                if (requestItem == null)
                {
                    existingItem.IsActive = false;
                    continue;
                }

                existingItem.Workflow_ID = requestItem.WorkflowId;
                existingItem.Planned_Qty = (uint)requestItem.PlannedQty;
            }

            foreach (var requestItem in dto.Items
                    .Where(x => x.BatchTopPartId == 0))
                {
                    var inactiveItem = inactiveItems.FirstOrDefault(x =>
                        x.TopPart_ID == requestItem.TopPartId &&
                        x.Workflow_ID == requestItem.WorkflowId);

                    if (inactiveItem is not null)
                    {
                        inactiveItem.Planned_Qty =
                            (uint)requestItem.PlannedQty;
                        inactiveItem.IsActive = true;
                        continue;
                    }

                    _db.ProductionBatchTopParts.Add(
                        new ProductionBatchTopPart
                        {
                            Batch_ID = batch.ID,
                            TopPart_ID = requestItem.TopPartId,
                            Workflow_ID = requestItem.WorkflowId,
                            Planned_Qty = (uint)requestItem.PlannedQty,
                            Done_Qty = 0,
                            IsPriority = false,
                            IsActive = true
                        });
                }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }

    }
}
