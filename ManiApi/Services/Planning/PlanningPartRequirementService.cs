using ManiApi.Data;
using ManiApi.Models;
using ManaApp.Shared.DTOs.Planning;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Planning
{
    public class PlanningPartRequirementService
    {
        private readonly AppDbContext _db;

        public PlanningPartRequirementService(AppDbContext db)
        {
            _db = db;
        }

        private async Task<Dictionary<int, decimal>>
            GetDraftWorkflowQuantitiesAsync()
        {
            return await _db.ProductionPlanningDraftItems
                .AsNoTracking()
                .Where(item =>
                    item.IsActive &&
                    item.Draft.IsActive &&
                    item.Draft.Status ==
                        ProductionPlanningDraftStatus.Draft)
                .GroupBy(item => item.Workflow_ID)
                .Select(group => new
                {
                    WorkflowId = group.Key,
                    Quantity = group.Sum(item =>
                        (decimal)item.Planned_Qty)
                })
                .ToDictionaryAsync(
                    row => row.WorkflowId,
                    row => row.Quantity);
        }

        private async Task<Dictionary<int, List<PartComponentRow>>>
            LoadComponentGraphAsync(IEnumerable<int> rootWorkflowIds)
        {
            var result = new Dictionary<int, List<PartComponentRow>>();
            var loadedWorkflowIds = new HashSet<int>();
            var pendingWorkflowIds = rootWorkflowIds.ToHashSet();

            while (pendingWorkflowIds.Count > 0)
            {
                var workflowIds = pendingWorkflowIds
                    .Where(id => !loadedWorkflowIds.Contains(id))
                    .ToList();

                pendingWorkflowIds.Clear();

                if (workflowIds.Count == 0)
                    break;

                var rows = await (
                    from component in _db.WorkflowComponents.AsNoTracking()
                    join topPart in _db.TopParts.AsNoTracking()
                        on component.TopPartId equals (uint?)topPart.Id
                    where workflowIds.Contains(component.WorkflowId)
                        && component.IsActive
                        && component.ComponentType == 1
                        && topPart.IsActive
                    select new PartComponentRow
                    {
                        WorkflowId = component.WorkflowId,
                        TopPartId = topPart.Id,
                        TopPartType = topPart.TopPartType,
                        ReferencedWorkflowId = component.ReferencedWorkflowId,
                        Quantity = component.Quantity
                    })
                    .ToListAsync();

                foreach (var workflowId in workflowIds)
                {
                    result[workflowId] = rows
                        .Where(row => row.WorkflowId == workflowId)
                        .ToList();

                    loadedWorkflowIds.Add(workflowId);
                }

                foreach (var referencedWorkflowId in rows
                    .Where(row => row.ReferencedWorkflowId.HasValue)
                    .Select(row => row.ReferencedWorkflowId!.Value))
                {
                    if (!loadedWorkflowIds.Contains(referencedWorkflowId))
                        pendingWorkflowIds.Add(referencedWorkflowId);
                }
            }

            return result;
        }

        public async Task<Dictionary<int, int>>
            CalculateDraftPartQuantitiesAsync()
        {
            var rootQuantities = await GetDraftWorkflowQuantitiesAsync();

            if (rootQuantities.Count == 0)
                return [];

            var graph = await LoadComponentGraphAsync(rootQuantities.Keys);
            var totals = new Dictionary<int, decimal>();

            void AddWorkflow(
                int workflowId,
                decimal parentQuantity,
                HashSet<int> workflowPath)
            {
                if (!workflowPath.Add(workflowId))
                {
                    throw new InvalidOperationException(
                        $"Workflow ķēdē atrasts cikls. Workflow ID: {workflowId}.");
                }

                if (graph.TryGetValue(workflowId, out var components))
                {
                    foreach (var component in components)
                    {
                        if (component.Quantity != decimal.Truncate(component.Quantity))
                        {
                            throw new InvalidOperationException(
                                $"PART daudzums BOM nav vesels skaitlis. " +
                                $"TopPart ID: {component.TopPartId}.");
                        }

                        var requiredQuantity =
                            parentQuantity * component.Quantity;

                        if (component.TopPartType == TopPartType.Part)
                        {
                            totals[component.TopPartId] =
                                totals.GetValueOrDefault(component.TopPartId) +
                                requiredQuantity;
                        }

                        if (component.ReferencedWorkflowId.HasValue)
                        {
                            AddWorkflow(
                                component.ReferencedWorkflowId.Value,
                                requiredQuantity,
                                workflowPath);
                        }
                    }
                }

                workflowPath.Remove(workflowId);
            }

            foreach (var root in rootQuantities)
            {
                AddWorkflow(
                    root.Key,
                    root.Value,
                    new HashSet<int>());
            }

            return totals.ToDictionary(
                row => row.Key,
                row => checked((int)row.Value));
        }

        private async Task<Dictionary<uint, int>>
            CalculateStartedQuantitiesAsync()
        {
            return await _db.TasksNew
                .AsNoTracking()
                .Where(task =>
                    task.IsActive &&
                    task.Started_At.HasValue &&
                    !_db.TaskNewDependencies.Any(dependency =>
                        dependency.TaskNew_ID == task.ID))
                .GroupBy(task => task.ProductionExecution_ID)
                .Select(group => new
                {
                    ExecutionId = group.Key,
                    Quantity = group.Sum(task => task.Quantity)
                })
                .ToDictionaryAsync(
                    row => row.ExecutionId,
                    row => row.Quantity);
        }

        private async Task<Dictionary<uint, int>>
            CalculateFinishedQuantitiesAsync()
        {
            return await (
                from movement in _db.StockMovementsNew.AsNoTracking()

                join task in _db.TasksNew.AsNoTracking()
                    on movement.ProducedByTaskNew_ID equals (uint?)task.ID

                join node in _db.WorkflowNodes.AsNoTracking()
                    on movement.WorkflowNode_ID equals (int?)node.Id

                where movement.IsActive &&
                    movement.Movement_Type == StockMovementType.PRODUCTION &&
                    movement.Quantity > 0 &&
                    node.NodeType == (byte)WorkflowNodeType.Finish

                group movement by task.ProductionExecution_ID
                into executionGroup

                select new
                {
                    ExecutionId = executionGroup.Key,
                    Quantity = executionGroup.Sum(x => x.Quantity)
                })
                .ToDictionaryAsync(
                    row => row.ExecutionId,
                    row => row.Quantity);
        }

        public async Task<(
            Dictionary<int, int> Waiting,
            Dictionary<int, int> InProduction)>
            CalculateApprovedPartProgressAsync()
        {
            var startedByExecution =
                await CalculateStartedQuantitiesAsync();

            var finishedByExecution =
                await CalculateFinishedQuantitiesAsync();

            var executions = await _db.ProductionExecutions
                .AsNoTracking()
                .Where(execution =>
                    execution.IsActive &&
                    execution.Status != ProductionExecutionStatus.SCRAPPED &&
                    execution.TopPart!.TopPartType == TopPartType.Part)
                .Select(execution => new
                {
                    execution.ID,
                    execution.TopPart_ID,
                    execution.Quantity
                })
                .ToListAsync();

            var waiting = new Dictionary<int, int>();
            var inProduction = new Dictionary<int, int>();

            foreach (var execution in executions)
            {
                var started = Math.Clamp(
                    startedByExecution.GetValueOrDefault(execution.ID),
                    0,
                    execution.Quantity);

                var finished = Math.Clamp(
                    finishedByExecution.GetValueOrDefault(execution.ID),
                    0,
                    started);

                var waitingQuantity = execution.Quantity - started;
                var inProductionQuantity = started - finished;

                waiting[execution.TopPart_ID] =
                    waiting.GetValueOrDefault(execution.TopPart_ID) +
                    waitingQuantity;

                inProduction[execution.TopPart_ID] =
                    inProduction.GetValueOrDefault(execution.TopPart_ID) +
                    inProductionQuantity;
            }

            return (waiting, inProduction);
        }

        public async Task<(
            Dictionary<int, int> Stock,
            Dictionary<int, int> Reserved,
            Dictionary<int, int> Free)>
            CalculateStockSummaryAsync()
        {
            var stock = await _db.StockMovementsNew
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.TopPart!.TopPartType == TopPartType.Part)
                .GroupBy(x => x.TopPart_ID)
                .Select(group => new
                {
                    TopPartId = group.Key,
                    Quantity = group.Sum(x => x.Quantity)
                })
                .ToDictionaryAsync(
                    x => x.TopPartId,
                    x => x.Quantity);

            var reserved = await _db.ProductionReservations
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Status == ProductionReservationStatus.ACTIVE &&
                    x.TopPart!.TopPartType == TopPartType.Part)
                .GroupBy(x => x.TopPart_ID)
                .Select(group => new
                {
                    TopPartId = group.Key,
                    Quantity = group.Sum(x =>
                        x.ReservedQuantity -
                        x.ConsumedQuantity -
                        x.ReleasedQuantity)
                })
                .ToDictionaryAsync(
                    x => x.TopPartId,
                    x => x.Quantity);

            var topPartIds = stock.Keys
                .Concat(reserved.Keys)
                .Distinct();

            var free = topPartIds.ToDictionary(
                topPartId => topPartId,
                topPartId => Math.Max(
                    0,
                    stock.GetValueOrDefault(topPartId) -
                    reserved.GetValueOrDefault(topPartId)));

            return (stock, reserved, free);
        }

        public async Task<List<PlanningPartStockDetailDto>>
            GetStockDetailsAsync(int topPartId)
        {
            var sources = await _db.StockMovementsNew
                .AsNoTracking()
                .Where(movement =>
                    movement.TopPart_ID == topPartId &&
                    movement.IsActive &&
                    movement.Movement_Type == StockMovementType.PRODUCTION &&
                    movement.Quantity > 0 &&
                    movement.WorkflowNode_ID.HasValue &&
                    (movement.WorkflowNode!.NodeType ==
                        (byte)WorkflowNodeType.Wip ||
                     movement.WorkflowNode.NodeType ==
                        (byte)WorkflowNodeType.Finish) &&
                    !_db.StockMovementsNew.Any(reversal =>
                        reversal.IsActive &&
                        reversal.ReversalOfMovement_ID == movement.ID))
                .Select(movement => new
                {
                    movement.ID,
                    movement.Quantity,
                    WorkflowId = movement.ProductionBatchTopPart != null
                        ? (int?)movement.ProductionBatchTopPart.Workflow_ID
                        : movement.ProducedByTaskNew != null &&
                          movement.ProducedByTaskNew.ProductionExecution != null
                            ? (int?)movement.ProducedByTaskNew
                                .ProductionExecution.Workflow_ID
                            : null,
                    NodeId = movement.WorkflowNode_ID!.Value,
                    NodeType = movement.WorkflowNode!.NodeType,
                    NodeName = movement.WorkflowNode.Name ?? ""
                })
                .Where(source => source.WorkflowId.HasValue)
                .ToListAsync();

            if (sources.Count == 0)
                return [];

            var sourceIds = sources.Select(source => source.ID).ToList();

            var linkedQuantities = await _db.StockMovementsNew
                .AsNoTracking()
                .Where(movement =>
                    movement.IsActive &&
                    movement.SourceMovement_ID.HasValue &&
                    sourceIds.Contains(movement.SourceMovement_ID.Value))
                .GroupBy(movement => movement.SourceMovement_ID!.Value)
                .Select(group => new
                {
                    SourceMovementId = group.Key,
                    Quantity = group.Sum(movement => movement.Quantity)
                })
                .ToDictionaryAsync(
                    row => row.SourceMovementId,
                    row => row.Quantity);

            var reservedQuantities = await _db.ProductionReservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.IsActive &&
                    reservation.Status == ProductionReservationStatus.ACTIVE &&
                    sourceIds.Contains(reservation.SourceMovement_ID))
                .GroupBy(reservation => reservation.SourceMovement_ID)
                .Select(group => new
                {
                    SourceMovementId = group.Key,
                    Quantity = group.Sum(reservation =>
                        reservation.ReservedQuantity -
                        reservation.ConsumedQuantity -
                        reservation.ReleasedQuantity)
                })
                .ToDictionaryAsync(
                    row => row.SourceMovementId,
                    row => row.Quantity);

            var workflowIds = sources
                .Select(source => source.WorkflowId!.Value)
                .Distinct()
                .ToList();

            var workflowVersions = await _db.Workflows
                .AsNoTracking()
                .Where(workflow => workflowIds.Contains(workflow.Id))
                .ToDictionaryAsync(
                    workflow => workflow.Id,
                    workflow => workflow.WorkflowVersion);

            return sources
                .Select(source => new
                {
                    WorkflowId = source.WorkflowId!.Value,
                    source.NodeId,
                    source.NodeType,
                    source.NodeName,
                    StockQty = source.Quantity +
                        linkedQuantities.GetValueOrDefault(source.ID),
                    ReservedQty =
                        reservedQuantities.GetValueOrDefault(source.ID)
                })
                .GroupBy(source => new
                {
                    source.WorkflowId,
                    source.NodeId,
                    source.NodeType,
                    source.NodeName
                })
                .Select(group => new PlanningPartStockDetailDto
                {
                    WorkflowId = group.Key.WorkflowId,
                    WorkflowVersion = workflowVersions.GetValueOrDefault(
                        group.Key.WorkflowId),
                    NodeType = group.Key.NodeType ==
                        (byte)WorkflowNodeType.Wip ? "WIP" : "FINISH",
                    NodeName = group.Key.NodeName,
                    StockQty = group.Sum(row => row.StockQty),
                    ReservedQty = group.Sum(row => row.ReservedQty),
                    FreeQty = Math.Max(
                        0,
                        group.Sum(row => row.StockQty) -
                        group.Sum(row => row.ReservedQty))
                })
                .Where(row => row.StockQty != 0 || row.ReservedQty != 0)
                .OrderByDescending(row => row.WorkflowVersion)
                .ThenBy(row => row.NodeType)
                .ThenBy(row => row.NodeName)
                .ToList();
        }

        public async Task<List<PlanningPartRequirementDetailDto>>
            GetProductionRequirementDetailsAsync(int topPartId)
        {
            var rows = await (
                from requirement in _db.ProductionRequirements.AsNoTracking()
                join sourceTopPart in _db.TopParts.AsNoTracking()
                    on requirement.SourceTopPart_ID equals sourceTopPart.Id
                join processComponent in
                    _db.WorkflowProcessComponents.AsNoTracking()
                    on requirement.WorkflowProcessComponent_ID
                    equals processComponent.Id
                join component in _db.WorkflowComponents.AsNoTracking()
                    on processComponent.WorkflowComponentId equals component.Id
                join processNode in _db.WorkflowNodes.AsNoTracking()
                    on processComponent.ProcessNodeId equals processNode.Id
                join node in _db.WorkflowNodes.AsNoTracking()
                    on component.RequiredWorkflowNodeId equals (int?)node.Id
                join workflow in _db.Workflows.AsNoTracking()
                    on node.WorkflowId equals workflow.Id
                where requirement.IsActive &&
                    requirement.RequiredTopPart_ID == topPartId &&
                    sourceTopPart.IsActive &&
                    component.IsActive &&
                    processNode.IsActive &&
                    node.IsActive &&
                    workflow.IsActive &&
                    (node.NodeType == (byte)WorkflowNodeType.Wip ||
                     node.NodeType == (byte)WorkflowNodeType.Finish)
                select new RequirementDetailRow
                {
                    RequirementId = requirement.ID,
                    ParentRequirementId = requirement.ParentRequirement_ID,
                    BatchCode = requirement.ProductionBatchTopPart != null &&
                        requirement.ProductionBatchTopPart!.IsActive &&
                        requirement.ProductionBatchTopPart!.Batch != null &&
                        requirement.ProductionBatchTopPart!.Batch!.IsActive
                            ? requirement.ProductionBatchTopPart!.Batch!.Batch_Code
                            : null,
                    SourceTopPartName = sourceTopPart.TopPartName,
                    SourceTopPartCode = sourceTopPart.TopPartCode,
                    WorkflowVersion = workflow.WorkflowVersion,
                    ProcessName = processNode.Name ?? "",
                    NodeType = node.NodeType,
                    NodeName = node.Name ?? "",
                    GrossQuantity = requirement.GrossQuantity,
                    StockCoveredQuantity = requirement.StockCoveredQuantity,
                    IncomingCoveredQuantity = requirement.IncomingCoveredQuantity,
                    NetQuantity = requirement.NetQuantity
                })
                .ToListAsync();

            if (rows.Count == 0)
                return [];

            var requirementLinks = await _db.ProductionRequirements
                .AsNoTracking()
                .Where(requirement => requirement.IsActive)
                .Select(requirement => new
                {
                    requirement.ID,
                    requirement.ParentRequirement_ID,
                    BatchCode = requirement.ProductionBatchTopPart != null &&
                        requirement.ProductionBatchTopPart!.IsActive &&
                        requirement.ProductionBatchTopPart!.Batch != null &&
                        requirement.ProductionBatchTopPart!.Batch!.IsActive
                            ? requirement.ProductionBatchTopPart!.Batch!.Batch_Code
                            : null
                })
                .ToDictionaryAsync(row => row.ID);

            string ResolveBatchCode(RequirementDetailRow row)
            {
                if (!string.IsNullOrWhiteSpace(row.BatchCode))
                    return row.BatchCode;

                var visited = new HashSet<uint> { row.RequirementId };
                var parentId = row.ParentRequirementId;

                while (parentId.HasValue && visited.Add(parentId.Value) &&
                    requirementLinks.TryGetValue(parentId.Value, out var parent))
                {
                    if (!string.IsNullOrWhiteSpace(parent.BatchCode))
                        return parent.BatchCode;

                    parentId = parent.ParentRequirement_ID;
                }

                return "";
            }

            return rows
                .Select(row => new PlanningPartRequirementDetailDto
                {
                    BatchCode = ResolveBatchCode(row),
                    SourceTopPartName = row.SourceTopPartName,
                    SourceTopPartCode = row.SourceTopPartCode,
                    WorkflowVersion = row.WorkflowVersion,
                    ProcessName = row.ProcessName,
                    NodeType = row.NodeType == (byte)WorkflowNodeType.Wip
                        ? "WIP"
                        : "FINISH",
                    NodeName = row.NodeName,
                    GrossQuantity = row.GrossQuantity,
                    StockCoveredQuantity = row.StockCoveredQuantity,
                    IncomingCoveredQuantity = row.IncomingCoveredQuantity,
                    NetQuantity = row.NetQuantity
                })
                .OrderBy(row => row.BatchCode)
                .ThenBy(row => row.SourceTopPartName)
                .ThenByDescending(row => row.WorkflowVersion)
                .ThenBy(row => row.NodeName)
                .ToList();
        }

        private sealed class PartComponentRow
        {
            public int WorkflowId { get; set; }
            public int TopPartId { get; set; }
            public TopPartType TopPartType { get; set; }
            public int? ReferencedWorkflowId { get; set; }
            public decimal Quantity { get; set; }
        }

        private sealed class RequirementDetailRow
        {
            public uint RequirementId { get; set; }
            public uint? ParentRequirementId { get; set; }
            public string? BatchCode { get; set; }
            public string SourceTopPartName { get; set; } = "";
            public string SourceTopPartCode { get; set; } = "";
            public int WorkflowVersion { get; set; }
            public string ProcessName { get; set; } = "";
            public byte NodeType { get; set; }
            public string NodeName { get; set; } = "";
            public int GrossQuantity { get; set; }
            public int StockCoveredQuantity { get; set; }
            public int IncomingCoveredQuantity { get; set; }
            public int NetQuantity { get; set; }
        }

    }
}
