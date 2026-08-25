using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.TopParts
{
    public class TopPartWorkflowService
    {
        private readonly AppDbContext _db;

        public TopPartWorkflowService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<int> CreateInitialWorkflowAsync(
                int topPartId,
                string topPartName)
            {
               var workflow = new ManiApi.Models.Workflow
                {
                    TopPartId = (uint)topPartId,
                    WorkflowVersion = 1,
                    Status = WorkflowStatus.Draft,
                    VersionId = null,
                    ParentNodeId = null,
                    Name = $"{topPartName} - V1",
                    CreatedDate = DateTime.Now,
                    IsCurrent = false,
                    IsActive = true
                };

                _db.Workflows.Add(workflow);
                await _db.SaveChangesAsync();

                var partNode = new WorkflowNode
                {
                    WorkflowId = workflow.Id,
                    NodeType = (byte)WorkflowNodeType.Part,
                    Name = topPartName,
                    TopPartId = (uint)topPartId,
                    SortOrder = 10,
                    IsActive = true
                };

                var finishNode = new WorkflowNode
                {
                    WorkflowId = workflow.Id,
                    NodeType = (byte)WorkflowNodeType.Finish,
                    Name = "FINISH",
                    TopPartId = (uint)topPartId,
                    SortOrder = 20,
                    IsActive = true
                };

                _db.WorkflowNodes.AddRange(partNode, finishNode);
                await _db.SaveChangesAsync();

                // _db.WorkflowNodeConnections.Add(
                //     new WorkflowNodeConnection
                //     {
                //         FromNodeId = partNode.Id,
                //         ToNodeId = finishNode.Id
                //     });

                // await _db.SaveChangesAsync();

                return workflow.Id;
            }

            public async Task PropagateReleasedWorkflowAsync(int releasedWorkflowId)
                {
                    var releasedWorkflow = await _db.Workflows
                        .FirstOrDefaultAsync(x =>
                            x.Id == releasedWorkflowId &&
                            x.Status == WorkflowStatus.Released &&
                            x.IsCurrent &&
                            x.IsActive);

                    if (releasedWorkflow == null)
                        return;

                    var dependentComponents = await _db.WorkflowComponents
                        .Where(x =>
                            x.ComponentType == 1 &&
                            x.ReferencedWorkflowId == releasedWorkflow.ParentWorkflowId &&
                            x.IsActive)
                        .ToListAsync();

                    if (dependentComponents.Count == 0)
                        return;

                    var dependentWorkflowIds = dependentComponents
                        .Select(x => x.WorkflowId)
                        .Distinct()
                        .ToList();

                    var dependentWorkflows = await _db.Workflows
                        .Where(x =>
                            dependentWorkflowIds.Contains(x.Id) &&
                            x.Status == WorkflowStatus.Released &&
                            x.IsCurrent &&
                            x.IsActive)
                        .ToListAsync();

                    if (dependentWorkflows.Count == 0)
                        return;

                    foreach (var dependentWorkflow in dependentWorkflows)
                        {
                            var nextVersion = await _db.Workflows
                                .Where(x => x.TopPartId == dependentWorkflow.TopPartId)
                                .MaxAsync(x => (int?)x.WorkflowVersion) ?? 0;

                            nextVersion++;

                            var releasedDate =
                                releasedWorkflow.ReleasedDate ?? DateTime.Now;

                            var changeDescription =
                                $"Atjaunots {releasedDate:dd.MM.yyyy}: {releasedWorkflow.Name}.";

                            var description = string.IsNullOrWhiteSpace(dependentWorkflow.Description)
                                ? changeDescription
                                : $"{dependentWorkflow.Description}{Environment.NewLine}{changeDescription}";
                            
                            dependentWorkflow.IsCurrent = false;

                            var newWorkflow = new ManiApi.Models.Workflow
                            {
                                TopPartId = dependentWorkflow.TopPartId,
                                WorkflowVersion = nextVersion,
                                Status = WorkflowStatus.Released,
                                ParentWorkflowId = dependentWorkflow.Id,
                                VersionId = dependentWorkflow.VersionId,
                                ParentNodeId = dependentWorkflow.ParentNodeId,
                                Name = $"{dependentWorkflow.Name.Split(" - V")[0]} - V{nextVersion}",
                                CreatedDate = DateTime.Now,
                                ReleasedDate = DateTime.Now,
                                Description = description,
                                IsCurrent = true,
                                IsActive = true
                            };

                            _db.Workflows.Add(newWorkflow);
                            await _db.SaveChangesAsync();

                            var sourceNodes = await _db.WorkflowNodes
                                .Where(x =>
                                    x.WorkflowId == dependentWorkflow.Id &&
                                    x.IsActive)
                                .OrderBy(x => x.SortOrder)
                                .ToListAsync();

                            var nodeIdMap = new Dictionary<int, int>();

                            foreach (var sourceNode in sourceNodes)
                            {
                                var newNode = new WorkflowNode
                                {
                                    WorkflowId = newWorkflow.Id,
                                    ParentNodeId = sourceNode.Id,
                                    NodeType = sourceNode.NodeType,
                                    Name = sourceNode.Name,
                                    TopPartId = sourceNode.TopPartId,
                                    WorkCenterId = sourceNode.WorkCenterId,
                                    EstimatedMinutes = sourceNode.EstimatedMinutes,
                                    Comments = sourceNode.Comments,
                                    SortOrder = sourceNode.SortOrder,
                                    IsActive = true
                                };

                                _db.WorkflowNodes.Add(newNode);
                                await _db.SaveChangesAsync();

                                nodeIdMap[sourceNode.Id] = newNode.Id;
                            }

                        var sourceNodeIds = sourceNodes
                            .Select(x => x.Id)
                            .ToList();

                        var sourceConnections = await _db.WorkflowNodeConnections
                            .Where(x =>
                                sourceNodeIds.Contains(x.FromNodeId) &&
                                sourceNodeIds.Contains(x.ToNodeId))
                            .ToListAsync();

                        foreach (var sourceConnection in sourceConnections)
                        {
                            _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                            {
                                FromNodeId = nodeIdMap[sourceConnection.FromNodeId],
                                ToNodeId = nodeIdMap[sourceConnection.ToNodeId]
                            });
                        }

                        await _db.SaveChangesAsync();

                    var sourceComponents = await _db.WorkflowComponents
                        .Where(x =>
                            x.WorkflowId == dependentWorkflow.Id &&
                            x.IsActive)
                        .ToListAsync();

                    var componentIdMap = new Dictionary<int, int>();

                    foreach (var sourceComponent in sourceComponents)
                    {
                        int? requiredWorkflowNodeId = sourceComponent.RequiredWorkflowNodeId;

                        if (sourceComponent.ReferencedWorkflowId == releasedWorkflow.ParentWorkflowId &&
                            sourceComponent.RequiredWorkflowNodeId.HasValue)
                        {
                            requiredWorkflowNodeId = await _db.WorkflowNodes
                                .Where(x =>
                                    x.WorkflowId == releasedWorkflow.Id &&
                                    x.ParentNodeId == sourceComponent.RequiredWorkflowNodeId.Value &&
                                    x.IsActive)
                                .Select(x => (int?)x.Id)
                                .FirstOrDefaultAsync();
                        }

                        if (sourceComponent.ComponentType == 1 &&
                                sourceComponent.RequiredWorkflowNodeId.HasValue &&
                                requiredWorkflowNodeId == null)
                            {
                                throw new InvalidOperationException(
                                    "Jaunajā Workflow versijā nav atrasts atbilstošais WIP/FINISH mezgls.");
                            }
                        
                        var newComponent = new WorkflowComponent
                        {
                            WorkflowId = newWorkflow.Id,
                            ComponentType = sourceComponent.ComponentType,
                            TopPartId = sourceComponent.TopPartId,
                            ItemId = sourceComponent.ItemId,
                            ReferencedWorkflowId =
                                sourceComponent.ReferencedWorkflowId == releasedWorkflow.ParentWorkflowId
                                    ? releasedWorkflow.Id
                                    : sourceComponent.ReferencedWorkflowId,
                            RequiredWorkflowNodeId = requiredWorkflowNodeId,
                            Quantity = sourceComponent.Quantity,
                            IsActive = true
                        };

                        _db.WorkflowComponents.Add(newComponent);
                        await _db.SaveChangesAsync();

                        componentIdMap[sourceComponent.Id] = newComponent.Id;
                    }

                    var sourceProcessComponents = await _db.WorkflowProcessComponents
                        .Where(x => sourceNodeIds.Contains(x.ProcessNodeId))
                        .ToListAsync();

                    foreach (var sourceProcessComponent in sourceProcessComponents)
                    {
                        _db.WorkflowProcessComponents.Add(new WorkflowProcessComponent
                        {
                            ProcessNodeId = nodeIdMap[sourceProcessComponent.ProcessNodeId],
                            WorkflowComponentId =
                                componentIdMap[sourceProcessComponent.WorkflowComponentId],
                            Quantity = sourceProcessComponent.Quantity
                        });
                    }

                    await _db.SaveChangesAsync();

                    await PropagateReleasedWorkflowAsync(newWorkflow.Id);

                }

            }

        public async Task<bool> HasDependentDraftsAsync(int workflowId)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x => x.Id == workflowId);

                if (workflow == null)
                    return false;

                var dependentTopPartIds = await _db.WorkflowComponents
                    .Where(x =>
                        x.ComponentType == 1 &&
                        x.ReferencedWorkflowId == workflow.Id &&
                        x.IsActive)
                    .Select(x => x.WorkflowId)
                    .Join(
                        _db.Workflows,
                        workflowId => workflowId,
                        w => w.Id,
                        (workflowId, w) => w.TopPartId)
                    .Distinct()
                    .ToListAsync();

                var hasDraft = await _db.Workflows
                    .AnyAsync(x =>
                        dependentTopPartIds.Contains(x.TopPartId) &&
                        x.Status == WorkflowStatus.Draft &&
                        x.IsActive);

                if (hasDraft)
                    return true;

                foreach (var topPartId in dependentTopPartIds)
                    {
                        var currentReleased = await _db.Workflows
                            .FirstOrDefaultAsync(x =>
                                x.TopPartId == topPartId &&
                                x.Status == WorkflowStatus.Released &&
                                x.IsCurrent &&
                                x.IsActive);

                        if (currentReleased != null &&
                            await HasDependentDraftsAsync(currentReleased.Id))
                        {
                            return true;
                        }
                    }
                

                return false;
            }

        public async Task UpdateFinishConnectionAsync(int workflowId)
            {
                var finishNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.WorkflowId == workflowId &&
                        x.NodeType == (byte)WorkflowNodeType.Finish &&
                        x.IsActive);

                if (finishNode == null)
                    return;

                await _db.WorkflowNodeConnections
                    .Where(x => x.ToNodeId == finishNode.Id)
                    .ExecuteDeleteAsync();

            }

    }
}