// WorkFlowAnalyzes.cs

using ManiApi.Models;
using ManiApi.DTOs.WorkFlow;

namespace ManiApi.Services.Workflow
{
    public class WorkflowFlowAnalyzer
    {
        private readonly List<WorkflowNode> _workflowNodes;
        private readonly List<WorkflowNodeConnection> _connections;
        private readonly List<ProductTopPart> _productParts;
        public IReadOnlyList<WorkflowNode> WorkflowNodes => _workflowNodes;

        public WorkflowFlowAnalyzer(
            List<WorkflowNode> workflowNodes,
            List<WorkflowNodeConnection> connections,
            List<ProductTopPart> productParts)
        {
            _workflowNodes = workflowNodes;
            _connections = connections;
            _productParts = productParts;
        }

    public int? FindFlowStartNodeId(int finishNodeId)

        {
            var currentId = finishNodeId;

            while (true)
            {
                var previous = _connections
                    .FirstOrDefault(x => x.ToNodeId == currentId);

                if (previous == null)
                    return null;

                var previousNode = GetNode(previous.FromNodeId);

                if (previousNode == null)
                    return null;

                if (previousNode.NodeType == 1 || previousNode.NodeType == 3)
                    return previousNode.Id;

                currentId = previousNode.Id;
            }
        }

        public WorkflowNode? GetFlowStartNode(int finishNodeId)
            {
                var startNodeId = FindFlowStartNodeId(finishNodeId);

                if (startNodeId == null)
                    return null;

                return GetNode(startNodeId.Value);
            }

        public WorkflowNode? GetFlowFinishNode(int finishNodeId)
            {
                return GetNode(finishNodeId);
            }

        public FlowInfoDto? GetFlowInfo(int finishNodeId)
            {
                var startNode = GetFlowStartNode(finishNodeId);

                if (startNode == null || !HasFlowFinish(startNode.Id))
                    return null;

                var isConsumed = _connections.Any(x => x.FromNodeId == finishNodeId);

                return new FlowInfoDto
                    {
                        StartNode = startNode,
                        FinishNode = GetFlowFinishNode(finishNodeId),
                        OwnerProductToPartId = startNode.ProductToPartId,
                        FlowType = startNode.NodeType == 3
                        ? AvailableFlowType.Merge
                        : _productParts.FirstOrDefault(x => x.Id == startNode.ProductToPartId)?.ParentProductTopPartId == null
                            ? AvailableFlowType.TopPart
                            : AvailableFlowType.SubPart,
                        IsConsumed = isConsumed
                    };
            }

        public List<int> GetNextNodeIds(int nodeId)
            {
                return _connections
                    .Where(x => x.FromNodeId == nodeId)
                    .Select(x => x.ToNodeId)
                    .ToList();
            }

        public List<int> GetPreviousNodeIds(int nodeId)
            {
                return _connections
                    .Where(x => x.ToNodeId == nodeId)
                    .Select(x => x.FromNodeId)
                    .ToList();
            }

        public bool CanReachNode(
                int fromNodeId,
                int targetNodeId,
                HashSet<int>? visited = null)
            {
                visited ??= new HashSet<int>();

                if (!visited.Add(fromNodeId))
                    return false;

                if (fromNodeId == targetNodeId)
                    return true;

                foreach (var nextId in GetNextNodeIds(fromNodeId))
                {
                    if (CanReachNode(nextId, targetNodeId, visited))
                        return true;
                }

                return false;
            }
        
        public bool HasFlowFinish(int startNodeId)
            {
                return CanReachFinish(
                    startNodeId,
                    new HashSet<int>());
            }

        private bool CanReachFinish(
            int nodeId,
            HashSet<int> visited)
        {
            if (!visited.Add(nodeId))
                return false;

            if (IsFinishNode(nodeId))
                return true;

            foreach (var nextId in GetNextNodeIds(nodeId))
            {
                if (CanReachFinish(nextId, visited))
                    return true;
            }

            return false;
        }

        private AvailableFlowDto CreateAvailableFlow(
            int startNodeId,
            int finishNodeId,
            AvailableFlowType flowType,
            string ownerName,
            int? ownerProductToPartId,
            bool isConsumed,
            bool isSelectable)
        {
            return new AvailableFlowDto
            {
                StartNodeId = startNodeId,
                FinishNodeId = finishNodeId,
                FlowType = flowType,
                OwnerName = ownerName,
                OwnerProductToPartId = ownerProductToPartId,
                DisplayName = $"{flowType} : {ownerName}",
                IsConsumed = isConsumed,
                IsSelectable = isSelectable
            };
        }

    public List<AvailableFlowDto> GetAvailableFlows()
        {
            var finishNodes = _workflowNodes
                .Where(x => x.NodeType == 4 && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var rows = finishNodes
                .Select(BuildAvailableFlow)
                .ToList();

            return rows;
        }

    private AvailableFlowDto BuildAvailableFlow(
                WorkflowNode finishNode)
        {
            var flow = GetFlowInfo(finishNode.Id);

                if (flow == null)
                    return CreateAvailableFlow(
                        startNodeId: 0,
                        finishNodeId: finishNode.Id,
                        flowType: AvailableFlowType.Unknown,
                        ownerName: finishNode.Name ?? "",
                        ownerProductToPartId: null,
                        isConsumed: false,
                        isSelectable: false);

                var isConsumed = flow.IsConsumed;
            
            var isSelectable = !flow.IsConsumed;


            if (flow == null)
            {
                return CreateAvailableFlow(
                    startNodeId: 0,
                    finishNodeId: finishNode.Id,
                    flowType: AvailableFlowType.Unknown,
                    ownerName: finishNode.Name ?? "",
                    ownerProductToPartId: null,
                    isConsumed: isConsumed,
                    isSelectable: isSelectable);
            }

            var startNode = flow.StartNode!;

            if (flow.FlowType == AvailableFlowType.Merge)
                {
                    return CreateAvailableFlow(
                        startNodeId: startNode.Id,
                        finishNodeId: finishNode.Id,
                        flowType: flow.FlowType,
                        ownerName: startNode.Name ?? "MERGE",
                        ownerProductToPartId: null,
                        isConsumed: isConsumed,
                        isSelectable: isSelectable);
                }

            if (flow.OwnerProductToPartId == null &&
                flow.FlowType != AvailableFlowType.Merge)
            {
                return CreateAvailableFlow(
                    startNodeId: startNode.Id,
                    finishNodeId: finishNode.Id,
                    flowType: AvailableFlowType.Unknown,
                    ownerName: finishNode.Name ?? "",
                    ownerProductToPartId: null,
                    isConsumed: isConsumed,
                    isSelectable: isSelectable);
            }
            
            return CreateAvailableFlow(
                startNodeId: startNode.Id,
                finishNodeId: finishNode.Id,
                flowType: flow.FlowType,
                ownerName: startNode.Name ?? "",
                ownerProductToPartId: flow.OwnerProductToPartId,
                isConsumed: isConsumed,
                isSelectable: isSelectable);
                
        }

        public WorkflowNode? GetProductFinishNode()
            {
                var productFinishNodes = _workflowNodes
                    .Where(x =>
                        x.NodeType == 4 &&
                        !_connections.Any(c => c.FromNodeId == x.Id))
                    .ToList();

                if (productFinishNodes.Count != 1)
                    return null;

                return productFinishNodes.Single();
            }

            public WorkflowNode? GetNode(int nodeId)
                {
                    return _workflowNodes.FirstOrDefault(x => x.Id == nodeId);
                }
            
            public bool IsFinishNode(int nodeId)
                {
                    var node = GetNode(nodeId);

                    return node != null && node.NodeType == 4;
                }

            public bool IsMergeNode(int nodeId)
                {
                    var node = GetNode(nodeId);

                    return node != null && node.NodeType == 3;
                }

            public bool IsPartNode(int nodeId)
                {
                    var node = GetNode(nodeId);

                    return node != null && node.NodeType == 1;
                }

             public bool IsProcessNode(int nodeId)
                {
                    var node = GetNode(nodeId);

                    return node != null && node.NodeType == 2;
                }   
            
            public WorkflowNode? FindTopPartFinish(
                WorkflowNode topPartNode)
                {
                    var visited = new HashSet<int>();


                    return FindTopPartFinishRecursive(
                        topPartNode.Id,
                        visited);
                }

            private WorkflowNode? FindTopPartFinishRecursive(
                int nodeId,
                HashSet<int> visited)
            {
                if (!visited.Add(nodeId))
                    return null;

                var currentNode = GetNode(nodeId)!;

                if (IsFinishNode(nodeId))
                {
                    var nextNodeIds = GetNextNodeIds(currentNode.Id);
                    if (nextNodeIds.Count == 0)
                        return currentNode;

                    if (nextNodeIds.All(IsMergeNode))

                    {
                        return currentNode;
                    }
                }

                
              foreach (var nextNodeId in GetNextNodeIds(nodeId))
                {
                    var finish = FindTopPartFinishRecursive(
                        nextNodeId,
                        visited);

                    if (finish != null)
                        return finish;
                }

                return null;
                
            }


        public WorkflowNode? FindParentPartNode(
            WorkflowNode mergeNode)
            {
                var visited = new HashSet<int>();
                var queue = new Queue<int>();

                queue.Enqueue(mergeNode.Id);

                while (queue.Count > 0)
                {
                    var currentId = queue.Dequeue();

                    if (!visited.Add(currentId))
                        continue;

                    var previousIds = GetPreviousNodeIds(currentId);

                    foreach (var previousId in previousIds)
                    {
                        var node = GetNode(previousId)!;

                        if (IsPartNode(node.Id))
                            return node;

                        queue.Enqueue(previousId);
                    }
                }

                return null;
            }

        
        public bool HasCycle(
                int nodeId,
                List<WorkflowNodeConnection> connections,
                HashSet<int> visited,
                HashSet<int> recursionStack)
            {
                if (recursionStack.Contains(nodeId))
                    return true;

                if (!visited.Add(nodeId))
                    return false;

                recursionStack.Add(nodeId);

                foreach (var nextId in GetNextNodeIds(nodeId))
                {
                    if (HasCycle(nextId, connections, visited, recursionStack))
                        return true;
                }

                recursionStack.Remove(nodeId);

                return false;
            }


    }

    
}