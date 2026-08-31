using ManiApi.Data;
using ManiApi.Models;
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

        private sealed class PartComponentRow
        {
            public int WorkflowId { get; set; }
            public int TopPartId { get; set; }
            public TopPartType TopPartType { get; set; }
            public int? ReferencedWorkflowId { get; set; }
            public decimal Quantity { get; set; }
        }

    }
}