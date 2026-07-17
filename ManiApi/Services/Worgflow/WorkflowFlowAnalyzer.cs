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


        public WorkflowNode? GetFlowStartNode(WorkflowNode finishNode)
            {
                return GetFlowStartNode(finishNode.Id);
            }

        public WorkflowNode? GetFlowStartNode(int finishNodeId)
            {
                var finishNode = GetNode(finishNodeId);

                if (finishNode == null)
                    return null;
                
                return FindFlowOwnerNode(finishNode);
            }

        public WorkflowNode? GetFlowFinishNode(int finishNodeId)
            {
                return GetNode(finishNodeId);
            }

        public FlowInfoDto? GetFlowInfoByFinish(int finishNodeId)
            {
                var finishNode = GetNode(finishNodeId);

                if (finishNode == null)
                    return null;

                var ownerNode = FindFlowOwnerNode(finishNode);
                

                if (ownerNode == null)
                    return null;

                if (ownerNode.NodeType != 3 && !HasFlowFinish(ownerNode))
                    return null;

                var flowFinish = GetFlowFinishNode(ownerNode);

                if (flowFinish == null || flowFinish.Id != finishNode.Id)
                    return null;

                var isConsumed = _connections.Any(x => x.FromNodeId == flowFinish.Id);

                return new FlowInfoDto
                    {
                        StartNode = ownerNode,
                        FinishNode = finishNode,
                        OwnerProductToPartId = GetOwnerProductToPartId(ownerNode),
                        FlowType = GetFlowType(ownerNode),
                        IsConsumed = isConsumed
                    };
            }

        private int? GetOwnerProductToPartId(WorkflowNode ownerNode)
            {
                return ownerNode.NodeType == 1
                    ? ownerNode.ProductToPartId
                    : null;
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

        public bool HasFlowFinish(WorkflowNode ownerNode)
            {
                return GetFlowFinishNode(ownerNode) != null;
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

        private AvailableFlowType GetFlowType(WorkflowNode ownerNode)
            {
                if (ownerNode.NodeType == 3)
                    return AvailableFlowType.Merge;

                return _productParts.FirstOrDefault(x => x.Id == ownerNode.ProductToPartId)?.ParentProductTopPartId == null
                    ? AvailableFlowType.TopPart
                    : AvailableFlowType.SubPart;
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
            var flow = GetFlowInfoByFinish(finishNode.Id);

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

            var startNode = flow.StartNode!;

// MERGE plūsmai pašlaik nav ProductToPart īpašnieka.
// Šo nosacījumu saglabājam līdz brīdim, kad tiks pārskatīta MERGE biznesa loģika.

            if (flow.FlowType == AvailableFlowType.Merge)
                {
                    return CreateAvailableFlow(
                        startNodeId: startNode.Id,
                        finishNodeId: finishNode.Id,
                        flowType: flow.FlowType,
                        ownerName: GetFlowOwnerName(flow),
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
                ownerName: GetFlowOwnerName(flow),
                ownerProductToPartId: flow.OwnerProductToPartId,
                isConsumed: isConsumed,
                isSelectable: isSelectable);
                
        }

        private string GetFlowOwnerName(FlowInfoDto flow)
        {
            return flow.FlowType == AvailableFlowType.Merge
                ? "MERGE"
                : flow.StartNode?.Name ?? "";
        }

        public WorkflowNode? GetProductFinishNode()
            {
                var productFinishNodes = _workflowNodes
                    .Where(x =>
                        x.NodeType == 4 &&
                        IsFlowFinishNode(x) &&
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
            
            private bool IsFlowFinishNode(WorkflowNode finishNode)
                {
                    var owner = FindFlowOwnerNode(finishNode);

                    if (owner == null)
                        return false;
                    
                    if (finishNode.NodeType != 4)
                        return false;

                    var nextNodeIds = GetNextNodeIds(finishNode.Id);

                    if (nextNodeIds.Count == 0)
                        return true;

                    if (owner.NodeType == 1)
                        {
                            return nextNodeIds.All(IsMergeNode);
                        }

                        if (owner.NodeType == 3)
                        {
                            return true;
                        }
                        
                    return false;
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
            
            public WorkflowNode? FindFlowFinish(
                WorkflowNode ownerNode)
                {
                    if (!IsFlowOwnerNode(ownerNode.Id))
                        return null;

                    var visited = new HashSet<int>();

                    return FindFlowFinishFromNode(
                        ownerNode.Id,
                        visited);
                }

            private WorkflowNode? FindFlowFinishRecursive(
                int nodeId,
                HashSet<int> visited)
            {
                if (!visited.Add(nodeId))
                    return null;

                var currentNode = GetNode(nodeId)!;

                if (IsFlowFinishNode(currentNode))
                    {
                        return currentNode;
                    }

                
              foreach (var nextNodeId in GetNextNodeIds(nodeId))
                {
                    var nextNode = GetNode(nextNodeId)!;

                    if (IsFlowOwnerNode(nextNode.Id) && !IsMergeNode(nextNode.Id))
                        continue;
                    
                    var finish = FindFlowFinishRecursive(
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
                    if (HasCycle(nextId, visited, recursionStack))
                        return true;
                }

                recursionStack.Remove(nodeId);

                return false;
            }

            public IEnumerable<WorkflowNode> GetFinishNodes()
                {
                    return _workflowNodes
                        .Where(x => x.NodeType == 4 && x.IsActive)
                        .OrderBy(x => x.SortOrder);
                }
            
            public IEnumerable<WorkflowNode> GetPartNodes()
                {
                    return _workflowNodes
                        .Where(x => x.NodeType == 1 && x.IsActive)
                        .OrderBy(x => x.SortOrder);
                }
            
            public IEnumerable<WorkflowNode> GetFlowOwnerNodes()
                {
                    return _workflowNodes
                        .Where(x =>
                            x.IsActive &&
                            IsFlowOwnerNode(x.Id))
                        .OrderBy(x => x.SortOrder);
                }

            public IEnumerable<WorkflowNode> GetMergeNodes()
                {
                    return _workflowNodes
                        .Where(x => x.NodeType == 3 && x.IsActive)
                        .OrderBy(x => x.SortOrder);
                }

            public WorkflowNode? GetPartNode(int productToPartId)
                {
                    return _workflowNodes.FirstOrDefault(x =>
                        x.NodeType == 1 &&
                        x.ProductToPartId == productToPartId &&
                        x.IsActive);
                }

            public WorkflowNode? GetFlowOwnerNodeByProductToPartId(int productToPartId)
                {
                    var partNode = GetPartNode(productToPartId);

                    if (partNode == null)
                        return null;

                    return FindFlowOwnerNode(partNode.Id);
                }

// TODO:
// Legacy metode.
// Pakāpeniski aizstāt ar GetFlowFinishNode(WorkflowNode ownerNode).

             public WorkflowNode? GetFlowFinishNodeByOwner(int productToPartId)
                {
                    var ownerNode = GetFlowOwnerNodeByProductToPartId(productToPartId);

                    if (ownerNode == null)
                        return null;

                    return GetFlowFinishNode(ownerNode);
                }  

            public WorkflowNode? GetFlowFinishNode(WorkflowNode ownerNode)
                {
                    
                    if (!IsFlowOwnerNode(ownerNode.Id))
                        return null;

                    return FindFlowFinish(ownerNode);
                }

// Central entry point for Flow traversal.
// If Flow traversal changes in the future, change it here.        
            private WorkflowNode? FindFlowFinishFromNode(
                    int nodeId,
                    HashSet<int> visited)
                {
                    return FindFlowFinishRecursive(nodeId, visited);
                }

        public FlowInfoDto? GetFlowInfoByOwner(int productToPartId)
            {
                var owner = GetFlowOwnerNodeByProductToPartId(productToPartId);

                if (owner == null)
                    return null;
                
                var flow = GetFlowInfoByFinish(
                    GetFlowFinishNode(owner)?.Id ?? 0);

                return flow;
            } 
        
        public WorkflowNode? FindFlowOwnerNode(int nodeId)
            {
                var currentId = nodeId;

                while (true)
                {
                    var node = GetNode(currentId);

                    if (node == null)
                        return null;

                    if (IsFlowOwnerNode(node.Id))
                        return node;

                    var previousNodes = GetPreviousNodeIds(currentId);

                    if (previousNodes.Count == 0)
                        return null;

                    if (previousNodes.Count > 1)
                    {
                        var mergeOwner = previousNodes
                            .Select(GetNode)
                            .FirstOrDefault(x => x != null && x.NodeType == 3);

                        if (mergeOwner != null)
                            return mergeOwner;

                        return null;
                    }

                    currentId = previousNodes[0];
                }
            }

        public WorkflowNode? FindFlowOwnerNode(WorkflowNode node)
            {
                return FindFlowOwnerNode(node.Id);
            }

        public bool IsFlowOwnerNode(int nodeId)
            {
                var node = GetNode(nodeId);

                return node != null &&
                    (node.NodeType == 1 || node.NodeType == 3);
            }

        public List<WorkflowNode> GetNextMergeNodes(int nodeId)
            {
                return GetNextNodeIds(nodeId)
                    .Select(GetNode)
                    .Where(x => x != null && IsMergeNode(x.Id))
                    .Cast<WorkflowNode>()
                    .ToList();
            }

        public bool HasPartNode(int productTopPartId)
            {
                return GetPartNode(productTopPartId) != null;
            }

    }

    
}