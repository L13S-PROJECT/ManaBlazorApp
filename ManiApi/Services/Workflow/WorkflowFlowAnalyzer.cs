// WorkFlowAnalyzes.cs

using ManiApi.Models;
using ManiApi.DTOs.WorkFlow;
using ManaApp.Shared.DTOs.Workflow;

namespace ManiApi.Services.Workflow
{
    public class WorkflowFlowAnalyzer
    {
        private readonly List<WorkflowNode> _workflowNodes;
        private readonly List<WorkflowNodeConnection> _connections;

        public IReadOnlyList<WorkflowNode> WorkflowNodes => _workflowNodes;
        private readonly List<WorkflowDependency> _dependencies;
        private readonly FlowRules _flowRules;

        public WorkflowFlowAnalyzer(
            List<WorkflowNode> workflowNodes,
            List<WorkflowNodeConnection> connections,
            List<WorkflowDependency> dependencies)
        {
            _workflowNodes = workflowNodes;
            _connections = connections;
            _dependencies = dependencies;
            _flowRules = new FlowRules(this);
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

        public FlowInfoDto? GetFlowInfo(AvailableFlowDto flow)
            {
                return GetFlowInfoByFinish(flow.FinishNodeId);
            }

        private AvailableFlowType GetFlowType(WorkflowNode ownerNode)
            {
                if (ownerNode.NodeType == 3)
                    return AvailableFlowType.Merge;

                return _dependencies.Any(x => x.NodeId == ownerNode.Id)
                    ? AvailableFlowType.SubPart
                    : AvailableFlowType.TopPart;
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
                .Select(BuildAvailableFlow)
                .ToList();

            return rows;
        }

    public List<AvailableFlowDto> GetAvailableFlows(
            int versionId,
            int flowOwnerNodeId)
        {
            return GetAvailableMergeFlows(
                versionId,
                flowOwnerNodeId);
        }
    
// TODO:
// Šī metode kļūs par vienīgo API ieejas punktu UI struktūras veidošanai.
// TechnologyStructureBuilder vairs nedrīkst analizēt Graph.

    public List<AvailableFlowDto> GetAvailableFlows(int versionId)
        {
            return GetFinishNodes()
                .Select(BuildAvailableFlow)
                .ToList();
        }

    public IEnumerable<WorkflowNode> GetNextNodes(int nodeId)
        {
            return GetNextNodeIds(nodeId)
                .Select(GetNode)
                .Where(x => x != null)
                .Cast<WorkflowNode>();
        }


    public List<AvailableFlowDto> GetAvailableMergeFlows(
                int versionId,
                int flowOwnerNodeId)
        {
            var currentOwner = GetNode(flowOwnerNodeId);

            if (currentOwner == null)
                return new List<AvailableFlowDto>();

            var currentFinish = GetFlowFinishNode(currentOwner);

            if (currentFinish == null)
                return new List<AvailableFlowDto>();

            var currentFlow = GetFlowInfoByFinish(currentFinish.Id);

            if (currentFlow == null)
                return new List<AvailableFlowDto>();


            return _workflowNodes
                .Where(x => x.NodeType == 4 && x.IsActive)
                .Select(x => GetFlowInfoByFinish(x.Id))
                .Where(x => x != null)
                .Where(flow =>
                    flow!.FinishNode!.Id != currentFlow.FinishNode!.Id &&
                    CanMergeFlows(currentFlow, flow))
                .Select(flow =>
                    CreateAvailableFlow(
                        flow!.StartNode!.Id,
                        flow.FinishNode!.Id,
                        flow.FlowType,
                        GetFlowOwnerName(flow),
                        flow.OwnerProductToPartId,
                        flow.IsConsumed,
                        true))
                .ToList();
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
        return _dependencies.Any(x =>
            x.NodeId == subPartFlow.StartNode!.Id &&
            IsFlowOwnerInHierarchy(
                topPartFlow.StartNode!.Id,
                x.DependsOnNodeId));
    }
    

    private bool HasSubPartFlows(FlowInfoDto flow)
        {
            if (flow.StartNode == null)
                return false;

            return _dependencies.Any(x =>
                x.DependsOnNodeId == flow.StartNode.Id);
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
            if (flow.StartNode != null &&
                IsContainerFlow(flow.StartNode))
            {
                return false;
            }

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
            
            if (firstFlow.StartNode == null || secondFlow.StartNode == null)
                return false;

            if (GetCurrentFlowOwner(firstFlow.StartNode) != firstFlow.StartNode)
                return false;

            if (GetCurrentFlowOwner(secondFlow.StartNode) != secondFlow.StartNode)
                return false;

            if (!firstFlow.IsFinished || !secondFlow.IsFinished)
                return false;

            if (firstFlow.IsConsumed || secondFlow.IsConsumed)
                return false;
            
            if ((firstFlow.StartNode != null && IsContainerFlow(firstFlow.StartNode)) ||
                (secondFlow.StartNode != null && IsContainerFlow(secondFlow.StartNode)))
            {
                return false;
            }
            
            if (!AreDependenciesConsumed(firstFlow) ||
                !AreDependenciesConsumed(secondFlow))
                return false;
            
            if (BelongsToTopPartFlow(firstFlow) &&
                BelongsToTopPartFlow(secondFlow))
            {
                return CanMergeTopParts(firstFlow, secondFlow);
            }

            if (BelongsToSubPartFlow(firstFlow) &&
                BelongsToSubPartFlow(secondFlow))
            {
                var firstDependency = _dependencies
                    .FirstOrDefault(x => x.NodeId == firstFlow.StartNode!.Id);

                var secondDependency = _dependencies
                    .FirstOrDefault(x => x.NodeId == secondFlow.StartNode!.Id);

                return firstDependency != null &&
                    secondDependency != null &&
                    firstDependency.DependsOnNodeId == secondDependency.DependsOnNodeId;
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
                    if (flow.StartNode == null)
                        return false;
                    
                    if (IsContainerFlow(flow.StartNode))
                        return false;
    
                    var currentOwner = GetCurrentFlowOwner(flow.StartNode);

                    if (currentOwner != flow.StartNode)
                        return false;

                    return flow.FinishNode != null &&
                            flow.IsFinished &&
                            !flow.IsConsumed &&
                            AreDependenciesConsumed(flow);
                }

    private bool AreDependenciesConsumed(FlowInfoDto flow)
        {
            if (flow.StartNode == null)
                return true;

            var dependencies = _dependencies
                .Where(x => x.NodeId == flow.StartNode.Id);

            foreach (var dependency in dependencies)
            {
                var parentFlowOwner = GetNode(dependency.DependsOnNodeId);

                if (parentFlowOwner != null &&
                    IsContainerFlow(parentFlowOwner))
                {
                    continue;
                }

                    if (parentFlowOwner == null)
                        return false;

                var currentOwner = GetCurrentFlowOwner(parentFlowOwner);

                if (currentOwner == null || currentOwner.Id == parentFlowOwner.Id)
                    return false;

            }

            return true;
        }

    private bool IsContainerFlow(WorkflowNode owner)
        {
            return !HasProcessNode(owner) &&
                GetFlowFinishNode(owner) == null &&
                HasDependentFlows(owner);
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

            // var ownerTopPartId = GetTopLevelProductPartId(owner.ProductToPartId);
            // _ = ownerTopPartId;

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
                        flowType: flow.FlowType,
                        ownerName: GetFlowOwnerName(flow),
                        ownerProductToPartId: flow.OwnerProductToPartId,
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
        

        private bool BelongsToSameTopPart(
                WorkflowNode firstOwner,
                WorkflowNode secondOwner)
            {
                var firstDependency = _dependencies
                    .FirstOrDefault(x => x.NodeId == firstOwner.Id);

                var secondDependency = _dependencies
                    .FirstOrDefault(x => x.NodeId == secondOwner.Id);

                return firstDependency != null &&
                    secondDependency != null &&
                    firstDependency.DependsOnNodeId == secondDependency.DependsOnNodeId;
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
                throw new InvalidOperationException(
                    "Tas pats Finished Flow izvēlēts vairākas reizes.");

            finishNodeIds = finishNodeIds
                .Distinct()
                .ToList();

            if (finishNodeIds.Count < 2)
                throw new InvalidOperationException(
                    "MERGE nepieciešami vismaz divi Finished Flow.");

            foreach (var finishId in finishNodeIds)
            {
                var flow = GetFlowInfoByFinish(finishId);

                if (flow == null)
                    throw new InvalidOperationException(
                        $"Flow {finishId} nav atrasts.");

                if (!AreDependenciesConsumed(flow))
                    {
                        throw new InvalidOperationException(
                            "Visām atkarīgajām plūsmām vispirms jābūt apvienotām ar MERGE.");
                    }
            }

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
        
// TODO:
// Šī validācija tiks aizstāta ar Flow atkarību analīzi.
// Validācija vairs nedrīkst balstīties uz ProductPart hierarhiju.
        private void ValidateChildFlowsFinished(WorkflowNode flowOwner)
            {
                var childFlows = _workflowNodes
                    .Where(x =>
                        x.NodeType == 1 &&
                        x.Id != flowOwner.Id &&
                        CanReachNode(flowOwner.Id, x.Id))
                    .ToList();

                foreach (var childFlow in childFlows)
                {
                    var finish = FindFlowFinish(childFlow);

                    if (finish == null)
                    {
                        throw new InvalidOperationException(
                            "Nevar pabeigt Flow, kamēr nav pabeigtas iekšējās plūsmas.");
                    }
                }
            }

// TODO:
// Šī validācija tiks apvienota kopējā Flow atkarību validācijā.
// MERGE un SUB PART tiks analizēti kā Flow atkarības grafā.
        private void ValidateMergeInputsFinished(WorkflowNode flowOwner)
            {
                var mergeNodes = _workflowNodes
                    .Where(x =>
                        x.NodeType == 3 &&
                        CanReachNode(flowOwner.Id, x.Id))
                    .ToList();

                foreach (var merge in mergeNodes)
                {
                    var previousNodes = GetPreviousNodeIds(merge.Id);

                    foreach (var previousId in previousNodes)
                    {
                        var previousNode = GetNode(previousId);

                        if (previousNode == null)
                            continue;

                        if (!IsFlowFinishNode(previousNode))
                            {
                                throw new InvalidOperationException(
                                    "MERGE ieejas plūsmām jābūt pabeigtām ar FINISH.");
                            }
                    }
                }
            }

        private void ValidateFlowDependenciesFinished(WorkflowNode flowOwner)
        {
            var visited = new HashSet<int>();

            ValidateFlowDependenciesRecursive(
                flowOwner.Id,
                flowOwner.Id,
                visited);

        }

        private void ValidateDependentFlowsMerged(WorkflowNode flowOwner)
            {
                var dependentNodes = _dependencies
                    .Where(x => x.DependsOnNodeId == flowOwner.Id)
                    .Select(x => GetNode(x.NodeId))
                    .Where(x => x != null)
                    .Cast<WorkflowNode>()
                    .ToList();

                if (dependentNodes.Count <= 1)
                    return;

                foreach (var node in dependentNodes)
                {
                    var finish = GetFlowFinishNode(node);

                    if (finish == null)
                        return;

                    var nextNodes = GetNextNodeIds(finish.Id);

                    if (!nextNodes.Any(IsMergeNode))
                    {
                        throw new InvalidOperationException(
                            "Atkarīgās plūsmas jāapvieno ar MERGE.");
                    }
                }
            }

        private void ValidateFlowDependenciesRecursive(
            int currentNodeId,
            int flowOwnerNodeId,
            HashSet<int> visited)
        {
            if (!visited.Add(currentNodeId))
                return;

            var currentNode = GetNode(currentNodeId);

                if (currentNode == null)
                    return;
   
            if (currentNode.Id != flowOwnerNodeId &&
                    IsFlowOwner(currentNode))
                {
                    var flow = GetFlowInfoByFinish(
                        GetFlowFinishNode(currentNode)?.Id ?? 0);

                    if (flow == null)
                        {
                            throw new InvalidOperationException(
                                "Neizdevās noteikt atkarīgās plūsmas informāciju.");
                        }
                    
                    if (flow.StartNode?.Id == flowOwnerNodeId)
                        {
                            return;
                        }

                    if (!flow.IsFinished)
                        {
                            throw new InvalidOperationException(
                                "Atkarīgajai plūsmai jābūt pabeigtai ar FINISH.");
                        }

                    if (flow.IsConsumed)
                        {
                            throw new InvalidOperationException(
                                "Atkarīgā plūsma jau izmantota citā MERGE.");
                        }
                    
                    if (flow.StartNode != null &&
                            IsContainerFlow(flow.StartNode))
                        {
                            return;
                        }

                    if (!AreDependenciesConsumed(flow))
                        {
                            throw new InvalidOperationException(
                                "Nav izpildītas Flow atkarības.");
                        }    

                    return;
                }

            foreach (var nextNodeId in GetNextNodeIds(currentNodeId))
                {
                    ValidateFlowDependenciesRecursive(
                        nextNodeId,
                        flowOwnerNodeId,
                        visited);
                }
        }

// TODO:
// Pirms FINISH izveides jāpārbauda visas šī Flow neatrisinātās
// atkarības Workflow grafā (SUB PART un MERGE), nevis ProductPart hierarhijā.
        public WorkflowNode? GetValidatedFlowLastNode(WorkflowNode flowOwner)
            {
                ValidateFlowOwner(flowOwner);

                ValidateFlowHasNoFinish(flowOwner.ProductToPartId ?? 0);
                
                // ValidateChildFlowsFinished(flowOwner);

                // ValidateMergeInputsFinished(flowOwner);

                ValidateFlowDependenciesFinished(flowOwner);
                // ValidateDependentFlowsMerged(flowOwner);

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

       private WorkflowNode? GetCurrentFlowOwner(WorkflowNode ownerNode)
            {
                if (!IsFlowOwner(ownerNode))
                    return null;

                var finish = GetFlowFinishNode(ownerNode);

                if (finish == null)
                    return ownerNode;
                
                var nextMerge = GetNextMergeNodes(finish.Id).FirstOrDefault();

                    if (nextMerge == null)
                        return ownerNode;

                return GetCurrentFlowOwner(nextMerge);
            }

        private bool IsFlowOwnerInHierarchy(
                int ancestorFlowOwnerId,
                int descendantFlowOwnerId)
            {
                if (ancestorFlowOwnerId == descendantFlowOwnerId)
                    return true;

                var dependency = _dependencies
                    .FirstOrDefault(x => x.NodeId == descendantFlowOwnerId);

                if (dependency == null)
                    return false;

                return IsFlowOwnerInHierarchy(
                    ancestorFlowOwnerId,
                    dependency.DependsOnNodeId);
            }

        public bool HasDependentFlows(WorkflowNode owner)
            {
                return _dependencies.Any(x => x.DependsOnNodeId == owner.Id);
            }

        public int GetDependentFlowCount(WorkflowNode owner)
            {
                return _dependencies.Count(x => x.DependsOnNodeId == owner.Id);
            }

        public bool HasProcessNode(WorkflowNode owner)
            {
                var visited = new HashSet<int>();

                return HasProcessNodeRecursive(owner.Id, visited);
            }

        private bool HasProcessNodeRecursive(
                int nodeId,
                HashSet<int> visited)
            {
                if (!visited.Add(nodeId))
                    return false;

                if (IsProcessNode(nodeId))
                    return true;

                foreach (var nextId in GetNextNodeIds(nodeId))
                {
                    var nextNode = GetNode(nextId);

                    if (nextNode == null)
                        continue;

                    if (IsFlowOwner(nextNode) && nextNode.Id != nodeId)
                        continue;

                    if (HasProcessNodeRecursive(nextId, visited))
                        return true;
                }

                return false;
            }

public List<FlowInfoDto> GetFlows()
{
    return _workflowNodes
        .Where(x => x.IsActive)
        .Where(IsFlowOwner)
        .Select(owner =>
        {
            var finish = GetFlowFinishNode(owner);

            if (finish == null)
                return null;

            return GetFlowInfoByFinish(finish.Id);
        })
        .Where(x => x != null)
        .Cast<FlowInfoDto>()
        .ToList();
}

        public WorkflowStructureDto BuildStructure()
            {
                return new WorkflowStructureDto
                {
                    Items = BuildItems()
                };
            }

        private List<WorkflowExplorerItemDto> BuildItems()
            {
                var result = new List<WorkflowExplorerItemDto>();
                var visitedFlowOwners = new HashSet<int>();

                foreach (var owner in GetStartFlowOwners())
                {
                    var item = BuildFlow(owner, visitedFlowOwners);

                    if (item != null)
                        result.Add(item);
                }

                return result;
            }

        private IEnumerable<WorkflowNode> GetStartFlowOwners()
            {
                return GetFlowOwnerNodes()
                    .Where(owner => owner.NodeType == 1)
                    .Where(owner => !HasParentFlow(owner))
                    .OrderBy(owner => owner.SortOrder);
            }
        
        private bool HasParentFlow(WorkflowNode owner)
            {
                return _dependencies.Any(d => d.NodeId == owner.Id);
            }

        private WorkflowStructureItemDto CreateNode(WorkflowGraphNodeDto graphNode)
            {
                return new WorkflowStructureItemDto
                {
                    Node = new WorkflowNodeDto
                    {
                        Id = graphNode.Node.Id,
                        WorkflowId = graphNode.Node.WorkflowId,
                        NodeType = graphNode.Node.NodeType,
                        Name = graphNode.Node.Name,
                        ProductToPartId = graphNode.Node.ProductToPartId,
                        WorkCenterId = graphNode.Node.WorkCenterId,
                        EstimatedMinutes = graphNode.Node.EstimatedMinutes,
                        Comments = graphNode.Node.Comments,
                        SortOrder = graphNode.Node.SortOrder
                    },
                    FlowLevel = 0,
                    HasValidationError = false
                };
            }
        
        // private void BuildStructureRecursive(
        //     Dictionary<int, WorkflowGraphNodeDto> graph,
        //     WorkflowGraphNodeDto current,
        //     WorkflowStructureItemDto parent,
        //     HashSet<int> visited)
        //     {
        //         if (!visited.Add(current.Node.Id))
        //             return;

        //         foreach (var next in current.Next
        //             .DistinctBy(x => x.Node.Id)
        //             .OrderBy(x => x.Node.SortOrder))
        //             {
        //                 var child = CreateNode(next);

        //                 parent.Children.Add(child);

        //                 if (!IsFlowOwner(next.Node))
        //                     {
        //                         BuildStructureRecursive(graph, next, child, visited);
        //                         continue;
        //                     }

        //                 BuildStructureRecursive(graph, next, child, visited);
        //             }
                
        //     }
    

        // private List<WorkflowNodeDto> BuildNodes()
        //     {
        //         return _workflowNodes.Select(x => new WorkflowNodeDto
        //         {
        //             Id = x.Id,
        //             WorkflowId = x.WorkflowId,
        //             NodeType = x.NodeType,
        //             Name = x.Name,
        //             ProductToPartId = x.ProductToPartId,
        //             WorkCenterId = x.WorkCenterId,
        //             EstimatedMinutes = x.EstimatedMinutes,
        //             Comments = x.Comments,
        //             SortOrder = x.SortOrder
        //         }).ToList();
        //     }

        // private List<WorkflowEdgeDto> BuildEdges()
        //     {
        //         return _connections.Select(x => new WorkflowEdgeDto
        //         {
        //             FromNodeId = x.FromNodeId,
        //             ToNodeId = x.ToNodeId
        //         }).ToList();
        //     }

//         private Dictionary<int, WorkflowGraphNodeDto> BuildGraph()
//             {
//                 var graph = new Dictionary<int, WorkflowGraphNodeDto>();

//                 foreach (var node in _workflowNodes)
//                 {
//                     graph[node.Id] = new WorkflowGraphNodeDto
//                     {
//                         Node = node
//                     };
//                 }

//                 foreach (var connection in _connections)
//                     {
//                         if (!graph.TryGetValue(connection.FromNodeId, out var from))
//                             continue;

//                         if (!graph.TryGetValue(connection.ToNodeId, out var to))
//                             continue;

//                         from.Next.Add(to);
//                         to.Previous.Add(from);
//                     }

//                     foreach (var dependency in _dependencies)
//                         {
//                             if (!graph.TryGetValue(dependency.DependsOnNodeId, out var parent))
//                                 continue;

//                             if (!graph.TryGetValue(dependency.NodeId, out var child))
//                                 continue;

//                             parent.Next.Add(child);
//                             child.Previous.Add(parent);
//                         }

//                 return graph;

                
//             }

// private IEnumerable<WorkflowNode> GetRootFlowOwners()
// {
//     foreach (var d in _dependencies)
// {
//     Console.WriteLine($"DEP: Node={d.NodeId} Parent={d.DependsOnNodeId}");
// }

//     return GetFlowOwnerNodes()
//         .Where(owner =>
//             !_dependencies.Any(d => d.NodeId == owner.Id))
//         .OrderBy(owner => owner.SortOrder);
// }

    private WorkflowExplorerItemDto? BuildFlow(
            WorkflowNode owner,
            HashSet<int> visitedFlowOwners,
            int level = 0)
        {
            if (!IsFlowOwner(owner))
                throw new InvalidOperationException("Node nav Flow Owner.");

            if (!visitedFlowOwners.Add(owner.Id))
                return null;

            var root = new WorkflowExplorerItemDto
                {
                    WorkflowNodeId = owner.Id,
                    Name = owner.Name ?? "",
                    FlowType = GetFlowType(owner)
                };
            
            root.Nodes.Add(new WorkflowExplorerNodeDto
                    {
                        WorkflowNodeId = owner.Id,
                        NodeType = owner.NodeType,
                        Name = owner.Name ?? ""
                    });

            root.Level = level;

            var currentItem = root;
            var currentNode = owner;
            WorkflowNode? finishNode = null;

            while (true)
            {
                var nextNodes = GetNextNodes(currentNode.Id)
                    .OrderBy(x => x.SortOrder)
                    .ToList();

                if (nextNodes.Count == 0)
                    break;

                var nextNode = nextNodes[0];

                if (nextNode == null)
                    break;

                if (IsFlowOwner(nextNode))
                    break;
                
                if (nextNode.NodeType != 2 && nextNode.NodeType != 4)
                    break;

               currentItem.Nodes.Add(new WorkflowExplorerNodeDto
                    {
                        WorkflowNodeId = nextNode.Id,
                        NodeType = nextNode.NodeType,
                        Name = nextNode.Name ?? ""
                    });

                currentNode = nextNode;
                
                if (IsFinishNode(nextNode.Id))
                {
                    finishNode = nextNode;
                    break;
                }
            }

            var dependentOwners = _dependencies
                .Where(x => x.DependsOnNodeId == owner.Id)
                .Select(x => GetNode(x.NodeId))
                .Where(x => x != null && IsFlowOwner(x))
                .Cast<WorkflowNode>()
                .DistinctBy(x => x.Id)
                .OrderBy(x => x.SortOrder);

            foreach (var dependentOwner in dependentOwners)
            {
                var dependentFlow = BuildFlow(
                    dependentOwner,
                    visitedFlowOwners,
                    level + 1);

                if (dependentFlow != null)
                    root.Children.Add(dependentFlow);
            }

            if (finishNode != null)
            {
                var mergeNodes = GetNextMergeNodes(finishNode.Id)
                    .OrderBy(x => x.SortOrder);

                foreach (var mergeNode in mergeNodes)
                {
                    var mergeFlow = BuildFlow(
                        mergeNode,
                        visitedFlowOwners,
                        level + 1);

                    if (mergeFlow != null)
                        root.Children.Add(mergeFlow);
                }
            }

            return root;
        }


    public List<WorkflowExplorerItemDto> BuildExplorer()
        {
            var result = new List<WorkflowExplorerItemDto>();
            var visited = new HashSet<int>();

            foreach (var owner in GetStartFlowOwners())
            {
                var flow = BuildFlow(owner, visited);

                if (flow != null)
                {
                    result.Add(flow);
                }
            }

            return result;
        }

        public WorkflowActionsDto GetAvailableActions(int selectedNodeId)
            {
                
                var selectedNode = GetNode(selectedNodeId);



                if (selectedNode == null)
                    return new WorkflowActionsDto();

                var node = FindFlowOwnerNode(selectedNode) ?? selectedNode;



                return new WorkflowActionsDto
                {
                    CanAddTopPart = false,
                    CanAddProcess = CanAddProcess(node),
                    CanAddSubPart =
                        IsFlowOwner(selectedNode) &&
                        !HasIncompleteProcess(),
                    CanAddFinish = node.NodeType == 1 || node.NodeType == 2
                };

            }

        private bool CanAddProcess(WorkflowNode node)
        {
            if (node.NodeType == 1 && IsContainerFlow(node))
                return false;

            return (node.NodeType == 1 || node.NodeType == 2)
                && !HasIncompleteProcess();
        }

        private bool HasIncompleteProcess()
            {
                return _workflowNodes.Any(x =>
                    x.NodeType == 2 &&
                    (
                        string.IsNullOrWhiteSpace(x.Name) ||
                        x.WorkCenterId == null ||
                        x.EstimatedMinutes == null
                    ));
            }



    }

    
}