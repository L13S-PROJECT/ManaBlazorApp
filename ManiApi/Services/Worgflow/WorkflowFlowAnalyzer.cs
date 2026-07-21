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
                var isFinished = IsFlowFinishNode(finishNode);

                return new FlowInfoDto
                    {
                        StartNode = ownerNode,
                        FinishNode = finishNode,
                        OwnerProductToPartId = GetOwnerProductToPartId(ownerNode),
                        FlowType = GetFlowType(ownerNode),
                        IsConsumed = isConsumed,
                        IsFinished = isFinished
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

// Šī metode atgriež visus individuāli derīgos Flow kandidātus.
// Savstarpējā MERGE saderība (vai divus Flow drīkst apvienot)
// tiek pārbaudīta atsevišķi.

    public List<AvailableFlowDto> GetAvailableMergeFlows(int versionId)
        {
            var finishNodes = _workflowNodes
                .Where(x => x.NodeType == 4 && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var rows = finishNodes
                .Select(BuildAvailableMergeFlow)
                .ToList();

            return rows;
        }
    
    public List<AvailableFlowDto> GetAvailableMergeFlows(
            int versionId,
            int flowOwnerNodeId)
        {
            return GetAvailableMergeFlows(versionId);
        }

    private bool BelongsToSubPartFlow(FlowInfoDto flow)
        {
            return flow.FlowType == AvailableFlowType.SubPart;
        }

    private bool BelongsToMergeFlow(FlowInfoDto flow)
        {
            return flow.FlowType == AvailableFlowType.Merge;
        }
    
    private bool BelongsToTopPartFlow(FlowInfoDto flow)
        {
            return flow.FlowType == AvailableFlowType.TopPart;
        }

    private bool CanMergeTopParts(
            FlowInfoDto firstFlow,
            FlowInfoDto secondFlow)
        {
            return !HasSubPartFlows(firstFlow) &&
                    !HasSubPartFlows(secondFlow);
        }

    private bool CanMergeTopPartWithSubPart(
            FlowInfoDto topPartFlow,
            FlowInfoDto subPartFlow)
        {
            if (topPartFlow.OwnerProductToPartId == null ||
                subPartFlow.OwnerProductToPartId == null)
            {
                return false;
            }

            return BelongsToSameTopPart(
                topPartFlow.StartNode!,
                subPartFlow.StartNode!);
        }

    private bool HasSubPartFlows(FlowInfoDto flow)
        {
            if (flow.StartNode?.ProductToPartId == null)
                return false;

            return _productParts.Any(x =>
                x.ParentProductTopPartId == flow.StartNode.ProductToPartId);
        }
    
    public bool HasAvailableMerge(int versionId)
        {
            return GetAvailableMergeFlows(versionId)
                .Count(x => x.IsSelectable) >= 2;
        }
    
    public bool HasAvailableMerge(
            int versionId,
            int flowOwnerNodeId)
        {
            var currentOwner = GetNode(flowOwnerNodeId);

            if (currentOwner == null)
                return false;

            var currentFinish = GetFlowFinishNode(currentOwner);

            if (currentFinish == null)
                return false;
            
            var currentFlow = GetFlowInfoByFinish(currentFinish.Id);

            if (currentFlow == null)
                return false;

            var otherFlows = GetAvailableMergeFlows(versionId)
                .Where(x => x.StartNodeId != flowOwnerNodeId);
            
            return otherFlows.Any(candidate =>
                {
                    var candidateFlow = GetFlowInfoByFinish(candidate.FinishNodeId);

                    return candidateFlow != null &&
                        CanMergeFlows(currentFlow, candidateFlow);
                });

        }


// Pārbauda tikai viena Flow gatavību dalībai MERGE.
// Savietojamība ar citiem Flow tiek pārbaudīta atsevišķi.

    private bool IsMergeCandidate(FlowInfoDto flow)
        {
            if (flow.StartNode == null)
                return false;
            
            return IsAvailableFlow(flow) &&
                    IsMergeFlowEligible(flow);
        }

    public bool CanMergeFlows(
            FlowInfoDto firstFlow,
            FlowInfoDto secondFlow)
        {
            // TODO:
            // Šeit atradīsies visu MERGE biznesa noteikumu salīdzināšana starp diviem Flow.

            if (firstFlow.FinishNode?.Id == secondFlow.FinishNode?.Id)
                return false;

            if (!firstFlow.IsFinished || !secondFlow.IsFinished)
                return false;

            if (firstFlow.IsConsumed || secondFlow.IsConsumed)
                return false;
            
            if (BelongsToTopPartFlow(firstFlow) &&
                BelongsToTopPartFlow(secondFlow))
            {
                return CanMergeTopParts(firstFlow, secondFlow);
            }

            if (BelongsToSubPartFlow(firstFlow) &&
                    BelongsToSubPartFlow(secondFlow))
                {
                    return BelongsToSameTopPart(
                        firstFlow.StartNode!,
                        secondFlow.StartNode!);
                }            

            if (BelongsToMergeFlow(firstFlow) && BelongsToSubPartFlow(secondFlow))
                return true;

            if (BelongsToSubPartFlow(firstFlow) && BelongsToMergeFlow(secondFlow))
                return true;

            if (BelongsToTopPartFlow(firstFlow) &&
                BelongsToSubPartFlow(secondFlow))
            {
                return CanMergeTopPartWithSubPart(firstFlow, secondFlow);
            }

            if (BelongsToSubPartFlow(firstFlow) &&
                BelongsToTopPartFlow(secondFlow))
            {
                return CanMergeTopPartWithSubPart(secondFlow, firstFlow);
            }

            if (BelongsToMergeFlow(firstFlow) &&
                BelongsToMergeFlow(secondFlow))
            {
                return true;
            }

            if (BelongsToTopPartFlow(firstFlow) &&
                BelongsToMergeFlow(secondFlow))
            {
                return true;
            }

            if (BelongsToMergeFlow(firstFlow) &&
                BelongsToTopPartFlow(secondFlow))
            {
                return true;
            }

        

// Visas pārējās Flow tipu kombinācijas pašlaik nav atļautas.

                return false;
        }

    public bool CanMerge(
        int firstFinishNodeId,
        int secondFinishNodeId)
    {
        var firstFlow = GetFlowInfoByFinish(firstFinishNodeId);
        var secondFlow = GetFlowInfoByFinish(secondFinishNodeId);

        if (firstFlow == null || secondFlow == null)
            return false;

        return CanMergeFlows(firstFlow, secondFlow);
    }

    private bool IsMergeFlowEligible(FlowInfoDto flow)
        {
            // Šeit tiek pārbaudīti tikai konkrētā Flow biznesa noteikumi.
            // Noteikumi, kuriem nepieciešams otrs Flow (piemēram, vai divus Flow drīkst
            // savienot vienā MERGE), tiks pārbaudīti atsevišķā validācijā.

            // Šeit atradīsies viena Flow biznesa validācija.
            // Divu Flow savstarpējā saderība tiek pārbaudīta CanMergeFlows().
            
            return true;
        }

    
    private bool HasOwnerProduct(FlowInfoDto flow)
        {
            return flow.OwnerProductToPartId != null;
        }


            private bool IsAvailableFlow(FlowInfoDto flow)
                {
                    return flow.FinishNode != null &&
                        flow.IsFinished &&
                        !flow.IsConsumed;
                }


    private bool HasOpenMergePoints(FlowInfoDto flow)
            {
                // pagaidām tikai stub
                return false;
            }

    private AvailableFlowDto BuildAvailableMergeFlow(
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
            
            var owner = flow.StartNode;

                if (owner == null)
                    return CreateAvailableFlow(
                        startNodeId: 0,
                        finishNodeId: finishNode.Id,
                        flowType: AvailableFlowType.Unknown,
                        ownerName: finishNode.Name ?? "",
                        ownerProductToPartId: null,
                        isConsumed: false,
                        isSelectable: false);

            var ownerTopPartId = GetTopLevelProductPartId(owner.ProductToPartId);
            _ = ownerTopPartId;

            var isConsumed = flow.IsConsumed;
            
            var isSelectable = IsMergeCandidate(flow);

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

            if (!isSelectable)
                {
                    return CreateAvailableFlow(
                        startNodeId: startNode.Id,
                        finishNodeId: finishNode.Id,
                        flowType: AvailableFlowType.Unknown,
                        ownerName: finishNode.Name ?? "",
                        ownerProductToPartId: null,
                        isConsumed: isConsumed,
                        isSelectable: false);
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
                var unconsumedFlowFinishes = GetUnconsumedFlowFinishes();

                if (unconsumedFlowFinishes.Count != 1)
                    return null;

                return unconsumedFlowFinishes[0];
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
            
            public bool IsFlowFinishNode(WorkflowNode finishNode)
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

                    if (IsFlowOwner(nextNode) && !IsMergeNode(nextNode.Id))
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
                            IsFlowOwner(x))
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

                    if (IsFlowOwner(node))
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

        private bool IsFlowOwnerNode(int nodeId)
            {
                return IsFlowOwner(GetNode(nodeId));
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

         public List<WorkflowNode> GetConsumedFlowFinishes()
            {
                return GetFinishNodes()
                    .Where(IsFlowFinishNode)
                    .Where(x => _connections.Any(c => c.FromNodeId == x.Id))
                    .ToList();
            }

        public List<WorkflowNode> GetUnconsumedFlowFinishes()
            {
                return GetFinishNodes()
                    .Where(IsFlowFinishNode)
                    .Where(x => !_connections.Any(c => c.FromNodeId == x.Id))
                    .ToList();
            }

        public bool CanStartSubPartFlow(WorkflowNode? selectedNode)
            {
                if (selectedNode == null)
                    return false;

                return IsFlowOwnerNode(selectedNode.Id);
            }

        public bool IsFlowOwner(WorkflowNode? node)
            {
                if (node == null)
                    return false;

                return node.NodeType == 1 || node.NodeType == 3;
            }

        private ProductTopPart? GetProductPart(int productToPartId)
            {
                return _productParts.FirstOrDefault(x => x.Id == productToPartId);
            }

        private ProductTopPart? GetTopLevelProductPart(int productToPartId)
            {
                var part = GetProductPart(productToPartId);

                while (part?.ParentProductTopPartId != null)
                {
                    part = GetProductPart(part.ParentProductTopPartId.Value);
                }

                return part;
            }

        private int? GetTopLevelProductPartId(int? productToPartId)
            {
                if (productToPartId == null)
                    return null;

                return GetTopLevelProductPart(productToPartId.Value)?.Id;
            }


        private bool BelongsToSameTopPart(
                WorkflowNode firstOwner,
                WorkflowNode secondOwner)
            {
                var firstTopPartId =
                    GetTopLevelProductPartId(firstOwner.ProductToPartId);

                var secondTopPartId =
                    GetTopLevelProductPartId(secondOwner.ProductToPartId);

                if (firstTopPartId == null || secondTopPartId == null)
                    return false;

                return firstTopPartId == secondTopPartId;
            }
        
        private bool CanBeMerged(
                FlowInfoDto flow,
                WorkflowNode finishNode)
            {
                _ = flow;
                _ = finishNode;

                return IsMergeCandidate(flow);
            }

        
        public List<int> NormalizeMergeSelection(
    int currentFlowId,
    IEnumerable<int> mergeFlowIds)
        {
            var finishNodeIds = mergeFlowIds
                .Append(currentFlowId)
                .ToList();

            if (finishNodeIds.Count != finishNodeIds.Distinct().Count())
                throw new InvalidOperationException("Tas pats Finished Flow izvēlēts vairākas reizes.");

            finishNodeIds = finishNodeIds
                .Distinct()
                .ToList();

            if (finishNodeIds.Count < 2)
                throw new InvalidOperationException("MERGE nepieciešami vismaz divi Finished Flow.");

                return finishNodeIds;
        }

    public WorkflowNode? GetFlowLastNode(int flowOwnerNodeId)
        {
            var lastNode = GetNode(flowOwnerNodeId);

                if (lastNode == null)
                    return null;

            while (true)
            {
                var nextNodeId = GetNextNodeIds(lastNode.Id).FirstOrDefault();

                if (nextNodeId == 0)
                    break;

                var next = GetNode(nextNodeId);

                if (next == null)
                    break;

                lastNode = next;
            }

            return lastNode;
        }

        public WorkflowNode CreateFinishNode(int workflowId, int sortOrder)
            {
                return new WorkflowNode
                {
                    WorkflowId = workflowId,
                    NodeType = 4,
                    Name = "FINISH",
                    SortOrder = sortOrder,
                    IsActive = true
                };
            }

        public WorkflowNodeConnection CreateConnection(int fromNodeId, int toNodeId)
                {
                    return new WorkflowNodeConnection
                    {
                        FromNodeId = fromNodeId,
                        ToNodeId = toNodeId
                    };
                }

        public void ValidateFlowOwner(WorkflowNode node)
            {
                if (!IsFlowOwner(node))
                    throw new InvalidOperationException(
                        "Flow Owner drīkst būt tikai PART vai MERGE mezgls.");
            }

        public void ValidateFlowHasNoFinish(int flowOwnerPartId)
            {
                if (GetFlowFinishNodeByOwner(flowOwnerPartId) != null)
                    throw new InvalidOperationException(
                        "Šai plūsmai FINISH jau eksistē.");
            }

        public WorkflowNode? GetValidatedFlowLastNode(WorkflowNode flowOwner)
            {
                ValidateFlowOwner(flowOwner);

                ValidateFlowHasNoFinish(flowOwner.ProductToPartId ?? 0);

                return GetFlowLastNode(flowOwner.Id);
            }

        public WorkflowNode CreateFinishForFlow(int workflowId, WorkflowNode lastNode)
            {
                return CreateFinishNode(
                    workflowId,
                    lastNode.SortOrder + 10);
            }

        public WorkflowNodeConnection CreateFinishConnection(
            WorkflowNode lastNode,
            WorkflowNode finishNode)
            {
                return CreateConnection(lastNode.Id, finishNode.Id);
            }

        public (WorkflowNode FinishNode, WorkflowNodeConnection Connection)
                BuildFinish(int workflowId, WorkflowNode lastNode)
            {
                var finishNode = CreateFinishForFlow(workflowId, lastNode);

                var connection = CreateFinishConnection(lastNode, finishNode);

                return (finishNode, connection);
            }

    }

    
}