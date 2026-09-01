using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Planning
{
    public class ProductionRequirementService
    {
        private readonly AppDbContext _db;

        public ProductionRequirementService(AppDbContext db)
        {
            _db = db;
        }

       public async Task<List<ProductionExecution>>
        CreateForExecutionsAsync(
            IReadOnlyCollection<ProductionExecution> executions)

        {
            if (executions.Count == 0)
                return [];

            var workflowIds = executions
                .Select(x => x.Workflow_ID)
                .Distinct()
                .ToList();

            var sourceTopPartIds = executions
                .Select(x => x.TopPart_ID)
                .Distinct()
                .ToList();

            var sourceTypes = await _db.TopParts
                .AsNoTracking()
                .Where(x => sourceTopPartIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.TopPartType);

            var components = await (
                from processComponent in
                    _db.WorkflowProcessComponents.AsNoTracking()

                join node in _db.WorkflowNodes.AsNoTracking()
                    on processComponent.ProcessNodeId equals node.Id

                join component in _db.WorkflowComponents.AsNoTracking()
                    on processComponent.WorkflowComponentId equals component.Id

                where workflowIds.Contains(node.WorkflowId)
                    && node.IsActive
                    && component.IsActive
                    && component.ComponentType == 1
                    && component.TopPartId.HasValue

                select new
                {
                    node.WorkflowId,
                    WorkflowProcessComponentId = processComponent.Id,
                    RequiredTopPartId = (int)component.TopPartId!.Value,
                    processComponent.Quantity
                })
                .ToListAsync();
            
            var parentRequirementIds = executions
                .Where(x => x.ProductionRequirement_ID.HasValue)
                .Select(x => x.ProductionRequirement_ID!.Value)
                .Distinct()
                .ToList();

            var parentSourceTypes = await _db.ProductionRequirements
                .AsNoTracking()
                .Where(x => parentRequirementIds.Contains(x.ID))
                .ToDictionaryAsync(x => x.ID, x => x.SourceType);

            var requirements = new List<ProductionRequirement>();

            foreach (var execution in executions)
            {
                var sourceType =
                    execution.ProductionRequirement_ID.HasValue
                        ? parentSourceTypes[
                            execution.ProductionRequirement_ID.Value]
                        : sourceTypes[execution.TopPart_ID] switch
                        {
                            TopPartType.Product =>
                                ProductionRequirementSourceType.PRODUCT,

                            TopPartType.SparePart =>
                                ProductionRequirementSourceType.SPARE_PART,

                            _ => throw new InvalidOperationException(
                                "Neatbalstīts ražošanas nepieciešamības avota tips.")
                        };

                foreach (var component in components
                    .Where(x => x.WorkflowId == execution.Workflow_ID))
                {
                    var grossQuantity =
                        component.Quantity * execution.Quantity;

                    if (grossQuantity <= 0 ||
                        grossQuantity != decimal.Truncate(grossQuantity))
                    {
                        throw new InvalidOperationException(
                            "Komponentes nepieciešamajam daudzumam jābūt veselam skaitlim.");
                    }

                    var quantity = checked((int)grossQuantity);

                    requirements.Add(new ProductionRequirement
                    {
                        SourceType = sourceType,
                        ProductionBatchTopPart_ID =
                            execution.ProductionBatchTopPart_ID,
                        ParentRequirement_ID =
                            execution.ProductionRequirement_ID,
                        SourceTopPart_ID = execution.TopPart_ID,
                        RequiredTopPart_ID = component.RequiredTopPartId,
                        WorkflowProcessComponent_ID =
                            component.WorkflowProcessComponentId,
                        GrossQuantity = quantity,
                        StockCoveredQuantity = 0,
                        IncomingCoveredQuantity = 0,
                        NetQuantity = quantity,
                        Priority = 0,
                        Created_At = DateTime.UtcNow,
                        IsActive = true
                    });
                }
            }

            _db.ProductionRequirements.AddRange(requirements);
                await _db.SaveChangesAsync();

            await ReserveStockFifoAsync(requirements);

            var incomingExecutions =
                await CreateIncomingExecutionsAsync(requirements);

            var descendantExecutions =
                await CreateForExecutionsAsync(incomingExecutions);

            incomingExecutions.AddRange(descendantExecutions);

            return incomingExecutions;
        }

        private async Task<List<StockSourceRow>> LoadStockSourcesAsync(
            IReadOnlyCollection<int> topPartIds)
        {
            var rows = await _db.StockMovementsNew
                .AsNoTracking()
                .Where(movement =>
                    topPartIds.Contains(movement.TopPart_ID) &&
                    movement.IsActive &&
                    movement.Movement_Type == StockMovementType.PRODUCTION &&
                    movement.Quantity > 0 &&
                    !_db.StockMovementsNew.Any(reversal =>
                        reversal.ReversalOfMovement_ID == movement.ID &&
                        reversal.IsActive))
                .OrderBy(movement => movement.Created_At)
                .ThenBy(movement => movement.ID)
                .Select(movement => new StockSourceRow
                {
                    MovementId = movement.ID,
                    TopPartId = movement.TopPart_ID,
                    ProductionBatchTopPartId =
                        movement.ProductionBatchTopPart_ID,
                    WorkflowId = movement.ProductionBatchTopPart != null
                        ? movement.ProductionBatchTopPart.Workflow_ID
                        : movement.ProducedByTaskNew != null &&
                        movement.ProducedByTaskNew.ProductionExecution != null
                            ? movement.ProducedByTaskNew
                                .ProductionExecution.Workflow_ID
                            : null,
                    WorkflowNodeId = movement.WorkflowNode_ID,
                    CreatedAt = movement.Created_At,

                    AvailableQuantity =
                        movement.Quantity
                        + (_db.StockMovementsNew
                            .Where(linked =>
                                linked.SourceMovement_ID == movement.ID &&
                                linked.IsActive)
                            .Sum(linked => (int?)linked.Quantity) ?? 0)
                        - (_db.ProductionReservations
                            .Where(reservation =>
                                reservation.SourceMovement_ID == movement.ID &&
                                reservation.IsActive &&
                                reservation.Status ==
                                    ProductionReservationStatus.ACTIVE)
                            .Sum(reservation => (int?)(
                                reservation.ReservedQuantity -
                                reservation.ConsumedQuantity -
                                reservation.ReleasedQuantity)) ?? 0)
                })
                .ToListAsync();

            return rows
                .Where(x => x.AvailableQuantity > 0)
                .ToList();
        }

        private async Task<List<ProductionExecution>>
            CreateIncomingExecutionsAsync(
                IReadOnlyCollection<ProductionRequirement> requirements)
        {
            var uncoveredRequirements = requirements
                .Where(x => x.NetQuantity > 0)
                .ToList();

            if (uncoveredRequirements.Count == 0)
                return [];

            var requirementIds = uncoveredRequirements
                .Select(x => x.ID)
                .ToList();

            var workflowIds = await (
                from requirement in _db.ProductionRequirements.AsNoTracking()

                join processComponent in
                    _db.WorkflowProcessComponents.AsNoTracking()
                    on requirement.WorkflowProcessComponent_ID
                    equals processComponent.Id

                join component in _db.WorkflowComponents.AsNoTracking()
                    on processComponent.WorkflowComponentId
                    equals component.Id

                where requirementIds.Contains(requirement.ID)
                    && component.ReferencedWorkflowId.HasValue

                select new
                {
                    requirement.ID,
                    WorkflowId = component.ReferencedWorkflowId!.Value
                })
                .ToDictionaryAsync(x => x.ID, x => x.WorkflowId);

            if (workflowIds.Count != uncoveredRequirements.Count)
            {
                throw new InvalidOperationException(
                    "Nenosegtai TopPart nepieciešamībai nav norādīta RELEASED Workflow.");
            }

            var executions = uncoveredRequirements
                .Select(requirement => new ProductionExecution
                {
                    ProductionBatchTopPart_ID = null,
                    ProductionRequirement_ID = requirement.ID,
                    TopPart_ID = requirement.RequiredTopPart_ID,
                    Workflow_ID = workflowIds[requirement.ID],
                    Quantity = requirement.NetQuantity,
                    Status = ProductionExecutionStatus.WAITING,
                    Created_At = DateTime.UtcNow,
                    IsActive = true
                })
                .ToList();

            foreach (var requirement in uncoveredRequirements)
            {
                requirement.IncomingCoveredQuantity +=
                    requirement.NetQuantity;
                requirement.NetQuantity = 0;
            }

            _db.ProductionExecutions.AddRange(executions);
            await _db.SaveChangesAsync();

            return executions;
        }

        private async Task ReserveStockFifoAsync(
            IReadOnlyCollection<ProductionRequirement> requirements,
            uint? preferredRequirementId = null)
        {
            var topPartIds = requirements
                .Select(x => x.RequiredTopPart_ID)
                .Distinct()
                .ToList();
            
            var requirementIds = requirements
                .Select(x => x.ID)
                .ToList();

            var requiredNodeIds = await (
                from requirement in _db.ProductionRequirements.AsNoTracking()

                join processComponent in
                    _db.WorkflowProcessComponents.AsNoTracking()
                    on requirement.WorkflowProcessComponent_ID
                    equals processComponent.Id

                join component in _db.WorkflowComponents.AsNoTracking()
                    on processComponent.WorkflowComponentId
                    equals component.Id

                where requirementIds.Contains(requirement.ID)
                    && component.RequiredWorkflowNodeId.HasValue

                select new
                {
                    requirement.ID,
                    RequiredWorkflowNodeId =
                        component.RequiredWorkflowNodeId!.Value
                })
                .ToDictionaryAsync(
                    x => x.ID,
                    x => x.RequiredWorkflowNodeId);

            // if (requiredNodeIds.Count != requirements.Count)
            // {
            //     throw new InvalidOperationException(
            //         "Vienai vai vairākām komponentēm nav norādīts nepieciešamais WIP/FINISH mezgls.");
            // }

            var missingRequirementIds = requirements
                .Where(x => !requiredNodeIds.ContainsKey(x.ID))
                .Select(x => x.ID)
                .ToList();

            if (missingRequirementIds.Count > 0)
            {
                throw new InvalidOperationException(
                    "Nav atrasts nepieciešamais WIP/FINISH mezgls " +
                    $"ProductionRequirement ID: {string.Join(", ", missingRequirementIds)}.");
            }

            var stockSources = await LoadStockSourcesAsync(topPartIds);
            var reservations = new List<ProductionReservation>();
            var now = DateTime.UtcNow;

            foreach (var requirement in requirements
                .OrderBy(x =>
                    x.ID == preferredRequirementId ? 0 : 1)
                .ThenByDescending(x => x.Priority)
                .ThenBy(x => x.Created_At)
                .ThenBy(x => x.ID))
            {
                var quantityToReserve =
                    requirement.NetQuantity +
                    requirement.IncomingCoveredQuantity;

                var quantityBeforeReservation = quantityToReserve;

                foreach (var source in stockSources.Where(x =>
                    x.TopPartId == requirement.RequiredTopPart_ID &&
                    x.WorkflowNodeId == requiredNodeIds[requirement.ID] &&
                    x.AvailableQuantity > 0))
                {
                    if (quantityToReserve == 0)
                        break;

                    var reservedQuantity = Math.Min(
                        quantityToReserve,
                        source.AvailableQuantity);

                    reservations.Add(new ProductionReservation
                    {
                        ProductionRequirement_ID = requirement.ID,
                        TopPart_ID = requirement.RequiredTopPart_ID,
                        SourceMovement_ID = source.MovementId,
                        SourceWorkflow_ID = source.WorkflowId,
                        SourceWorkflowNode_ID = source.WorkflowNodeId,
                        ReservedQuantity = reservedQuantity,
                        ConsumedQuantity = 0,
                        ReleasedQuantity = 0,
                        Status = ProductionReservationStatus.ACTIVE,
                        Created_At = now,
                        IsActive = true
                    });

                    source.AvailableQuantity -= reservedQuantity;
                    quantityToReserve -= reservedQuantity;
                }

                var newlyReservedQuantity =
                    quantityBeforeReservation - quantityToReserve;

                var coveredFromNet = Math.Min(
                    requirement.NetQuantity,
                    newlyReservedQuantity);

                requirement.NetQuantity -= coveredFromNet;

                var coveredFromIncoming =
                    newlyReservedQuantity - coveredFromNet;

                requirement.IncomingCoveredQuantity -= coveredFromIncoming;
                requirement.StockCoveredQuantity += newlyReservedQuantity;
            }

            _db.ProductionReservations.AddRange(reservations);
            await _db.SaveChangesAsync();
        }

        private async Task<List<TaskRequirementRow>>
                LoadTaskRequirementsAsync(TaskNew task)
            {
                var executionSource = await _db.ProductionExecutions
                    .Where(x => x.ID == task.ProductionExecution_ID)
                    .Select(x => new
                    {
                        x.ProductionBatchTopPart_ID,
                        x.ProductionRequirement_ID
                    })
                    .SingleAsync();

                var rows = await _db.ProductionRequirements
                    .Where(requirement =>
                        requirement.IsActive &&
                        (
                            (
                                executionSource.ProductionBatchTopPart_ID.HasValue &&
                        requirement.ProductionBatchTopPart_ID ==
                            executionSource.ProductionBatchTopPart_ID
                            ) ||
                            (
                                executionSource.ProductionRequirement_ID.HasValue &&
                        requirement.ParentRequirement_ID ==
                                executionSource.ProductionRequirement_ID
                            )
                        ) &&
                        requirement.WorkflowProcessComponent != null &&
                        requirement.WorkflowProcessComponent.ProcessNodeId ==
                            task.WorkflowNode_ID)
                    .Select(requirement => new TaskRequirementRow
                    {
                        Requirement = requirement,
                        ComponentQuantity =
                            requirement.WorkflowProcessComponent!.Quantity
                    })
                    .ToListAsync();

                var requirementIds = rows
                    .Select(x => x.Requirement.ID)
                    .ToList();

                var reservations = await _db.ProductionReservations
                    .Include(x => x.SourceMovement)
                    .Where(reservation =>
                        requirementIds.Contains(
                            reservation.ProductionRequirement_ID) &&
                        reservation.IsActive &&
                        reservation.Status ==
                            ProductionReservationStatus.ACTIVE)
                    .OrderBy(reservation => reservation.Created_At)
                    .ThenBy(reservation => reservation.ID)
                    .ToListAsync();

                foreach (var row in rows)
                {
                    row.Reservations = reservations
                        .Where(x =>
                            x.ProductionRequirement_ID == row.Requirement.ID)
                        .ToList();
                }

                return rows;
            }
        
        public async Task ReserveOutstandingAsync(
            int topPartId,
            uint? preferredRequirementId = null)
            {
                var requirements = await _db.ProductionRequirements
                    .Where(x =>
                        x.RequiredTopPart_ID == topPartId &&
                        x.IsActive &&
                            (
                                x.NetQuantity > 0 ||
                                x.IncomingCoveredQuantity > 0
                            ))
                    .OrderByDescending(x => x.Priority)
                    .ThenBy(x => x.Created_At)
                    .ThenBy(x => x.ID)
                    .ToListAsync();

                if (requirements.Count == 0)
                    return;

                await ReserveStockFifoAsync(
                    requirements,
                    preferredRequirementId);

            }

        private async Task<List<int>> LoadOutputNodeIdsAsync(
            int workflowId,
            int processNodeId)
        {
            var nodes = await _db.WorkflowNodes
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowId == workflowId &&
                    x.IsActive)
                .ToListAsync();

            var nodeById = nodes.ToDictionary(x => x.Id);
            var nodeIds = nodeById.Keys.ToHashSet();

            var connections = await _db.WorkflowNodeConnections
                .AsNoTracking()
                .Where(x =>
                    nodeIds.Contains(x.FromNodeId) &&
                    nodeIds.Contains(x.ToNodeId))
                .ToListAsync();

            var outgoingByNodeId = connections
                .GroupBy(x => x.FromNodeId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(connection => connection.ToNodeId)
                        .ToList());

            var pending = new Stack<(int NodeId, int? WipNodeId)>();

            foreach (var nodeId in
                outgoingByNodeId.GetValueOrDefault(processNodeId) ?? [])
            {
                pending.Push((nodeId, null));
            }

            var visited = new HashSet<(int NodeId, int? WipNodeId)>();
            var outputNodeIds = new List<int>();

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                if (!visited.Add(current) ||
                    !nodeById.TryGetValue(current.NodeId, out var node))
                {
                    continue;
                }

                if (node.NodeType == (byte)WorkflowNodeType.Finish)
                {
                    outputNodeIds.Add(node.Id);
                    continue;
                }

                if (node.NodeType == (byte)WorkflowNodeType.Process)
                {
                    if (current.WipNodeId.HasValue)
                        outputNodeIds.Add(current.WipNodeId.Value);

                    continue;
                }

                var nextWipNodeId =
                    node.NodeType == (byte)WorkflowNodeType.Wip
                        ? node.Id
                        : current.WipNodeId;

                var nextNodeIds =
                    outgoingByNodeId.GetValueOrDefault(node.Id) ?? [];

                if (nextNodeIds.Count == 0)
                {
                    if (nextWipNodeId.HasValue)
                        outputNodeIds.Add(nextWipNodeId.Value);

                    continue;
                }

                foreach (var nextNodeId in nextNodeIds)
                    pending.Push((nextNodeId, nextWipNodeId));
            }

            return outputNodeIds.Distinct().ToList();
        }

        public async Task<string?> ProduceTaskOutputsAsync(TaskNew task)
            {
                var execution = await _db.ProductionExecutions
                    .AsNoTracking()
                    .Where(x => x.ID == task.ProductionExecution_ID)
                    .Select(x => new
                    {
                        x.TopPart_ID,
                        x.Workflow_ID,
                        x.ProductionBatchTopPart_ID,
                        x.ProductionRequirement_ID
                    })
                    .SingleAsync();

                var outputNodeIds = await LoadOutputNodeIdsAsync(
                    execution.Workflow_ID,
                    task.WorkflowNode_ID);

                if (outputNodeIds.Count == 0)
                {
                    return "Pabeigtajam procesam nav piesaistīts WIP vai FINISH rezultāts.";
                }

                var existingOutputNodeIds = await _db.StockMovementsNew
                    .AsNoTracking()
                    .Where(x =>
                        x.ProducedByTaskNew_ID == task.ID &&
                        x.Movement_Type == StockMovementType.PRODUCTION &&
                        x.IsActive)
                    .Select(x => x.WorkflowNode_ID)
                    .ToListAsync();

                var movements = outputNodeIds
                    .Where(nodeId => !existingOutputNodeIds.Contains(nodeId))
                    .Select(nodeId => new StockMovementNew
                    {
                        TopPart_ID = execution.TopPart_ID,
                        ProductionBatchTopPart_ID =
                            execution.ProductionBatchTopPart_ID,
                        ProducedByTaskNew_ID = task.ID,
                        WorkflowNode_ID = nodeId,
                        Movement_Type = StockMovementType.PRODUCTION,
                        Quantity = task.Quantity,
                        Created_At = DateTime.UtcNow,
                        IsActive = true
                    })
                    .ToList();

                if (movements.Count == 0)
                    return null;

                _db.StockMovementsNew.AddRange(movements);
                await _db.SaveChangesAsync();

                await ReserveOutstandingAsync(
                    execution.TopPart_ID,
                    execution.ProductionRequirement_ID);

                return null;
            }

        private async Task<uint> LoadBatchIdForExecutionAsync(
            uint executionId)
        {
            var execution = await _db.ProductionExecutions
                .AsNoTracking()
                .Where(x => x.ID == executionId)
                .Select(x => new
                {
                    x.ProductionBatchTopPart_ID,
                    x.ProductionRequirement_ID
                })
                .SingleAsync();

            if (execution.ProductionBatchTopPart_ID.HasValue)
            {
                return await _db.ProductionBatchTopParts
                    .Where(x =>
                        x.ID == execution.ProductionBatchTopPart_ID.Value)
                    .Select(x => x.Batch_ID)
                    .SingleAsync();
            }

            var requirementId = execution.ProductionRequirement_ID;

            while (requirementId.HasValue)
            {
                var requirement = await _db.ProductionRequirements
                    .AsNoTracking()
                    .Where(x => x.ID == requirementId.Value)
                    .Select(x => new
                    {
                        x.ProductionBatchTopPart_ID,
                        x.ParentRequirement_ID
                    })
                    .SingleAsync();

                if (requirement.ProductionBatchTopPart_ID.HasValue)
                {
                    return await _db.ProductionBatchTopParts
                        .Where(x =>
                            x.ID ==
                            requirement.ProductionBatchTopPart_ID.Value)
                        .Select(x => x.Batch_ID)
                        .SingleAsync();
                }

                requirementId = requirement.ParentRequirement_ID;
            }

            throw new InvalidOperationException(
                "Ražošanas izpildei nav atrasta ražošanas partija.");
        }

        public async Task<string?> ConsumeForTaskAsync(TaskNew task)
        {
            var alreadyConsumed = await _db.StockMovementsNew.AnyAsync(x =>
                x.TaskNew_ID == task.ID &&
                x.Movement_Type == StockMovementType.CONSUMPTION &&
                x.IsActive);

            if (alreadyConsumed)
                return null;

            var productionBatchTopPartId =
                await _db.ProductionExecutions
                    .Where(x => x.ID == task.ProductionExecution_ID)
                    .Select(x => x.ProductionBatchTopPart_ID)
                    .SingleAsync();

            var batchId = await LoadBatchIdForExecutionAsync(
                task.ProductionExecution_ID);

            var rows = await LoadTaskRequirementsAsync(task);

            foreach (var row in rows)
            {
                var requiredQuantity =
                    row.ComponentQuantity * task.Quantity;

                if (requiredQuantity <= 0 ||
                    requiredQuantity != decimal.Truncate(requiredQuantity))
                {
                    return "Patērējamajam komponentes daudzumam jābūt veselam skaitlim.";
                }

                var reservedQuantity = row.Reservations
                    .Sum(x => x.RemainingQuantity);

                if (reservedQuantity < requiredQuantity)
                {
                    return $"Komponentei TopPart ID " +
                        $"{row.Requirement.RequiredTopPart_ID} " +
                        $"nav pietiekamas aktīvas rezervācijas.";
                }

                if (row.Reservations.Any(x => x.SourceMovement == null))
                    return "Rezervācijas noliktavas avota kustība nav atrasta.";
            }

            var movements = new List<StockMovementNew>();
            var now = DateTime.UtcNow;

            foreach (var row in rows)
            {
                var quantityToConsume = checked((int)(
                    row.ComponentQuantity * task.Quantity));

                foreach (var reservation in row.Reservations)
                {
                    if (quantityToConsume == 0)
                        break;

                    var consumedQuantity = Math.Min(
                        quantityToConsume,
                        reservation.RemainingQuantity);

                    movements.Add(new StockMovementNew
                    {
                        TopPart_ID = reservation.TopPart_ID,
                        ProductionBatchTopPart_ID =
                            productionBatchTopPartId,
                        TaskNew_ID = task.ID,
                        WorkflowNode_ID = task.WorkflowNode_ID,
                        RAL_Color_ID = reservation.SourceMovement!.RAL_Color_ID,
                        Movement_Type = StockMovementType.CONSUMPTION,
                        Quantity = -consumedQuantity,
                        SourceMovement_ID = reservation.SourceMovement_ID,
                        ProductionReservation_ID = reservation.ID,
                        ConsumedByBatch_ID = batchId,
                        Created_At = now,
                        IsActive = true
                    });

                    reservation.ConsumedQuantity += consumedQuantity;
                    quantityToConsume -= consumedQuantity;

                    if (reservation.RemainingQuantity == 0)
                    {
                        reservation.Status =
                            ProductionReservationStatus.CONSUMED;
                        reservation.Consumed_At = now;
                    }
                }
            }

            _db.StockMovementsNew.AddRange(movements);
            await _db.SaveChangesAsync();

            return null;
        }

        private sealed class StockSourceRow
            {
                public uint MovementId { get; set; }
                public int TopPartId { get; set; }
                public uint? ProductionBatchTopPartId { get; set; }
                public int? WorkflowId { get; set; }
                public int? WorkflowNodeId { get; set; }
                public DateTime CreatedAt { get; set; }
                public int AvailableQuantity { get; set; }
            }

        private sealed class TaskRequirementRow
            {
                public ProductionRequirement Requirement { get; set; } = null!;
                public decimal ComponentQuantity { get; set; }
                public List<ProductionReservation> Reservations { get; set; } = [];
            }

    }
}