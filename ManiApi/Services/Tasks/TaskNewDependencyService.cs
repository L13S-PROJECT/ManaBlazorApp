using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Tasks
{
    public class TaskNewDependencyService
    {
        private readonly AppDbContext _db;

        public TaskNewDependencyService(AppDbContext db)
        {
            _db = db;
        }

        public async Task CreateForExecutionAsync(
            uint productionExecutionId,
            int workflowId)
        {
            var nodes = await _db.WorkflowNodes
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowId == workflowId &&
                    x.IsActive)
                .ToListAsync();

            var nodeIds = nodes
                .Select(x => x.Id)
                .ToHashSet();

            var connections = await _db.WorkflowNodeConnections
                .AsNoTracking()
                .Where(x =>
                    nodeIds.Contains(x.FromNodeId) &&
                    nodeIds.Contains(x.ToNodeId))
                .ToListAsync();

            var tasks = await _db.TasksNew
                .Where(x =>
                    x.ProductionExecution_ID == productionExecutionId &&
                    x.IsActive)
                .ToListAsync();

            var nodeById = nodes.ToDictionary(x => x.Id);

            var taskByProcessNodeId = tasks.ToDictionary(
                x => x.WorkflowNode_ID);

            var incomingByNodeId = connections
                .GroupBy(x => x.ToNodeId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(connection => connection.FromNodeId)
                        .ToList());

            var dependencies = new List<TaskNewDependency>();

            foreach (var task in tasks)
            {
                var pendingNodes = new Stack<int>();
                var visitedNodes = new HashSet<int>();
                var dependencyTaskIds = new HashSet<uint>();

                if (incomingByNodeId.TryGetValue(
                    task.WorkflowNode_ID,
                    out var incomingNodeIds))
                {
                    foreach (var nodeId in incomingNodeIds)
                        pendingNodes.Push(nodeId);
                }

                while (pendingNodes.Count > 0)
                {
                    var nodeId = pendingNodes.Pop();

                    if (!visitedNodes.Add(nodeId))
                        continue;

                    if (!nodeById.TryGetValue(nodeId, out var node))
                        continue;

                    if (node.NodeType == (byte)WorkflowNodeType.Process)
                    {
                        if (!taskByProcessNodeId.TryGetValue(
                            nodeId,
                            out var dependencyTask))
                        {
                            throw new InvalidOperationException(
                                $"PROCESS mezglam ID {nodeId} nav izveidots Task.");
                        }

                        dependencyTaskIds.Add(dependencyTask.ID);
                        continue;
                    }

                    if (!incomingByNodeId.TryGetValue(
                        nodeId,
                        out var previousNodeIds))
                    {
                        continue;
                    }

                    foreach (var previousNodeId in previousNodeIds)
                        pendingNodes.Push(previousNodeId);
                }

                dependencies.AddRange(
                    dependencyTaskIds.Select(dependencyTaskId =>
                        new TaskNewDependency
                        {
                            TaskNew_ID = task.ID,
                            DependsOnTaskNew_ID = dependencyTaskId
                        }));
            }

            _db.TaskNewDependencies.AddRange(dependencies);
        }
    }
}
