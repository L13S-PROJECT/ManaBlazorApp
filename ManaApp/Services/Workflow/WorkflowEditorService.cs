using ManaApp.ViewModels.Workflow;
using ManaApp.DTOs.Workflow;
using ManaApp.Models;
using System.Net.Http.Json;
using ManaApp.Services.Common;

namespace ManaApp.Services.Workflow;

public class WorkflowEditorService
{
    private readonly WorkflowApiService _workflowApiService;
    private readonly WorkflowStateService _stateService;
    private readonly LookupService _lookupService;

    public WorkflowEditorService(
        WorkflowApiService workflowApiService,
        WorkflowStateService stateService,
        LookupService lookupService)
    {
        _workflowApiService = workflowApiService;
        _stateService = stateService;
        _lookupService = lookupService;
    }

    public async Task<WorkflowState?> LoadAsync(int versionId)
        {
            _stateService.Clear();

            var state = await _workflowApiService.LoadStateAsync(versionId);

            if (state == null)
                return null;

            _stateService.State.Workflow = state.Workflow;
            _stateService.State.InvalidFlowOwnerNodeIds = state.Workflow?.InvalidFlowOwnerNodeIds ?? new();
            _stateService.State.Graph = state.Graph;
            _stateService.State.PartNodeByProductToPartId = state.PartNodeByProductToPartId;
            _stateService.State.SelectedNode = state.SelectedNode;
            _stateService.State.AvailableFinishNodes = state.AvailableFinishNodes;
            _stateService.State.AvailableFlows = state.AvailableFlows;
            _stateService.State.ProductParts = state.ProductParts;
Console.WriteLine($"STATE PRODUCT PARTS = {_stateService.State.ProductParts.Count}");
            _stateService.State.WorkCenters =
                await _lookupService.LoadWorkCentersAsync();

            RefreshTechnologyTree();

            RefreshValidationState();

            return _stateService.State;
        }

        public bool TrySelectNode(int nodeId)
            {
                if (!_stateService.State.Graph.TryGetValue(nodeId, out var node))
                    return false;

                _stateService.State.SelectedNode = node;
                return true;
            }
        
        public bool CanAddProcess()
            {
                return SelectedNode != null &&
                    (SelectedNode.Node.NodeType == 1 ||
                        SelectedNode.Node.NodeType == 2);
            }
        
        public bool CanAddSubPart()
            {
                return SelectedNode != null &&
                    SelectedNode.Node.NodeType != 2 &&
                    SelectedNode.Node.NodeType != 4;
            }
        
        public bool CanAddTopPart()
            {
                return SelectedStructureItem == null;
            }

        public bool CanAddMerge()
            {
                return SelectedNode != null &&
                    SelectedNode.Node.NodeType is 1 or 3;
            }

        public bool CanAddFinish()
            {
                if (SelectedStructureItem == null)
                    return false;

                if (SelectedNode == null)
                    return false;

                if (SelectedNode.Node.NodeType == 1 ||
                    SelectedNode.Node.NodeType == 2)
                {
                    return GetCurrentFlowOwner() != null &&
                        GetCurrentFlowOwnerHasNoFinish();
                }

                return false;
            }
        
        private bool GetCurrentFlowOwnerHasNoFinish()
            {
                var owner = GetCurrentFlowOwner();

                if (owner == null)
                    return false;

                var ownerNode = FindPartNode(owner);

                if (ownerNode == null)
                    return false;

                return !State.Graph.Values.Any(x =>
                    x.Node.NodeType == 4 &&
                    x.Previous.Any(p => p.Node.Id == ownerNode.Node.Id));
            }

        public WorkflowState State => _stateService.Current;
        public WorkflowDto? Workflow => State.Workflow;
        public Dictionary<int, WorkflowGraphNode> Graph => State.Graph;
        public WorkflowGraphNode? SelectedNode => State.SelectedNode;
        public TechnologyTreeItem? SelectedTreeItem => State.SelectedTreeItem;
        public TechnologyStructureItem? SelectedStructureItem => State.SelectedStructureItem;
        public WorkflowPartModel? SelectedPart => SelectedStructureItem?.Part;
        
        public List<MergeFinishItem> AvailableFinishNodes =>
            _stateService.Current.AvailableFinishNodes;

        public void ClearFinishNodes()
            {
                State.AvailableFinishNodes.Clear();
            }
        
        public async Task SelectNodeAsync(int nodeId)
            {
                if (State.SelectedNode?.Node.NodeType == 3)
                {
                    var items = await _workflowApiService.LoadFinishNodesAsync(State.Workflow!.Workflow!.Id);

                    ClearFinishNodes();

                    if (items == null)
                        return;

                    FillFinishNodes(items);
                }
            }
        
        public async Task LoadAvailableFinishNodesAsync()
            {
                if (State.Workflow?.Workflow == null)
                    return;

                var items = await _workflowApiService.LoadFinishNodesAsync(State.Workflow.Workflow.Id);

                ClearFinishNodes();

                if (items == null)
                    return;

                FillFinishNodes(items);

            }

        public async Task LoadAvailableFlowsAsync()
            {
                if (State.Workflow?.Workflow == null)
                    return;

                var currentFlow = GetCurrentFlow();

                    if (currentFlow == null)
                    {
                        State.CanMergeCurrentFlow = false;
                        State.AvailableFlows.Clear();
                        return;
                    }

                    var flows = await _workflowApiService.LoadAvailableFlowsAsync(
                        State.Workflow.Workflow.Id,
                        currentFlow.FinishNodeId);

                State.AvailableFlows.Clear();

                foreach (var flow in flows)
                {
                    State.AvailableFlows.Add(new MergeFlowItem
                    {
                        Flow = flow
                    });
                }

                State.CanMergeCurrentFlow = State.AvailableFlows.Any();
            }

        public string NodeTypeName(int type)
        {
            return type switch
            {
                1 => "PART",
                2 => "PROCESS",
                3 => "MERGE",
                4 => "FINISH",
                _ => "?"
            };
        }

        public bool IsLoaded =>
            State.Workflow != null;
        
    public async Task LoadProductPartsToStateAsync(int versionId)
        {
            State.ProductParts = await _workflowApiService.LoadProductPartsAsync(versionId);
            State.SelectedTopPartId = 0;
        }
    
    public List<WorkflowPartModel> ProductParts =>
    State.ProductParts;

    public int SelectedTopPartId
    {
        get => State.SelectedTopPartId;
        set => State.SelectedTopPartId = value;
    }
   
    public async Task ReloadAsync()
        {
            if (Workflow?.Workflow == null)
                return;

            await LoadAsync(Workflow.Workflow.VersionId);
            
        }

    public async Task ReloadAndRestoreSelectionAsync()
        {
            var selectedPartId = SelectedPart?.ProductToPartId;

            var selectedFinishNodeId = State.SelectedFlow?.FinishNodeId;

            await ReloadAsync();

            if (selectedFinishNodeId.HasValue)
                {
                    State.SelectedFlow = State.AvailableFlows
                        .Select(x => x.Flow)
                        .FirstOrDefault(x => x.FinishNodeId == selectedFinishNodeId.Value);
                }

            if (selectedPartId.HasValue)
                {
                    RestoreSelectedStructureItem(selectedPartId.Value);
                }
        }
    
    public void RefreshTechnologyTree()
        {
            State.TechnologyTree = BuildTechnologyTree();

            State.TechnologyStructure = BuildTechnologyStructure();

            RefreshTechnologyExplorer();
        }

    private List<TechnologyStructureItem> BuildTechnologyStructure()
        {
            return new TechnologyStructureBuilder(State).Build();
        }
    
    public void RefreshTechnologyExplorer()
        {
            State.TechnologyExplorer = BuildTechnologyExplorer();
        }
    
    private TechnologyExplorerItem CreateExplorerPart(WorkflowPartModel part)
        {
            var item = new TechnologyExplorerItem
            {
                Part = part
            };

            var startNode = FindStartNode(part);

            if (startNode != null)
            {
                item.GraphNode = startNode;

                BuildPartFlow(
                    startNode,
                    item.Children);
            }

            return item;

        }
    
    
    private void BuildExplorerBranch(
    WorkflowGraphNode current,
    List<TechnologyExplorerItem> items)
        {           
            foreach (var next in current.Next
             .OrderBy(x => x.Node.NodeType == 1 ? 0 : 1))
                {
                
                    TechnologyExplorerItem item;

                    if (next.Node.NodeType == 1)
                    {
                        var part = State.ProductParts
                            .FirstOrDefault(x => x.ProductToPartId == next.Node.ProductToPartId);

                        item = new TechnologyExplorerItem
                        {
                            Part = part,
                            GraphNode = next
                        };
                    }
                    else
                    {
                        item = new TechnologyExplorerItem
                        {
                            GraphNode = next
                        };
                    }

                    items.Add(item);

                    BuildExplorerBranch(
                        next,
                        item.Children);
                }
        }
    
            private void BuildPartFlow(
                WorkflowGraphNode current,
                List<TechnologyExplorerItem> items)
                    {
                        
var attachedParts = State.ProductParts
    .Where(x => x.AttachToNodeId == current.Node.Id)
    .ToList();

foreach (var part in attachedParts)
{
    var subPartItem = CreateExplorerPart(part);

    items.Add(subPartItem);
}
                        
                        foreach (var next in current.Next)
                        {
                            var item = new TechnologyExplorerItem
                            {
                                GraphNode = next
                            };

                            items.Add(item);

                            BuildPartFlow(
                                next,
                                item.Children);
                        }
                    }
            
    private WorkflowGraphNode? FindStartNode(WorkflowPartModel part)
{
    var node = FindPartNode(part);

    return node;
}

    private List<TechnologyExplorerItem> BuildTechnologyExplorer()
        {
            var result = new List<TechnologyExplorerItem>();

            var roots = State.ProductParts
                .Where(x => x.ParentProductTopPartId == null)
                .OrderBy(x => x.TopPartName);

            foreach (var part in roots)
                {
                    result.Add(CreateExplorerPart(part));
                }

            return result;
        }


    public void RestoreSelectedTreeItem(int productToPartId)
        {
            var part = State.ProductParts
                .FirstOrDefault(x => x.ProductToPartId == productToPartId);

            if (part != null)
                {
                    SelectPart(part);
                }
        }

    public void RestoreSelectedStructureNode(int nodeId)
        {
            foreach (var root in State.TechnologyStructure)
            {
                var item = FindStructureNode(root, nodeId);

                if (item != null)
                {
                    SelectStructureItem(item);
                    return;
                }
            }
        }

    private TechnologyStructureItem? FindStructureNode(
            TechnologyStructureItem item,
            int nodeId)
        {
            if (item.Node?.Id == nodeId)
                return item;

            foreach (var child in item.Children)
            {
                var found = FindStructureNode(child, nodeId);

                if (found != null)
                    return found;
            }

            return null;
        }

    public void RestoreSelectedStructureItem(int productToPartId)
        {
            var item = FindStructureItem(
                State.TechnologyStructure,
                productToPartId);

            if (item != null)
            {
                SelectStructureItem(item);
            }
        }

    private TechnologyStructureItem? FindStructureItem(
    IEnumerable<TechnologyStructureItem> items,
    int productToPartId)
{
    foreach (var item in items)
    {
        if (item.Part?.ProductToPartId == productToPartId)
            return item;

        var child = FindStructureItem(
            item.PartChildren,
            productToPartId);

        if (child != null)
            return child;
    }

    return null;
}

    private List<TechnologyTreeItem> BuildTechnologyTree()
        {
            return new TechnologyTreeBuilder(State).Build();
        }
    
    private IEnumerable<WorkflowPartModel> GetAttachedParts(int nodeId)
        {
            return State.ProductParts
                .Where(x => x.AttachToNodeId == nodeId)
                .OrderBy(x => x.TopPartName);
        }

    private void BuildPartChildren(TechnologyTreeItem parent)
        {
            var children = State.ProductParts
                .Where(x => x.ParentProductTopPartId == parent.Part.ProductToPartId)
                .OrderBy(x => x.TopPartName);

            var list = children.ToList();

            for (int i = 0; i < list.Count; i++)
                {
                    var item = CreatePart(list[i]);

                    item.IsFlowChild = list[i].AttachToNodeId != null;

                    item.IsLastChild = i == list.Count - 1;

                    item.Parent = parent;
                    item.IsHierarchyChild = true;

                    parent.PartChildren.Add(item);

                    BuildPartChildren(item);
                }
        }

    private List<TechnologyTreeItem> NodeChildren(WorkflowGraphNode? node)
            {
                return node == null
                    ? new()
                    : BuildChildren(node, 1);
            }
    
    private WorkflowGraphNode? FindPartNode(WorkflowPartModel? part)
        {
            if (part == null)
                return null;

            return State.PartNodeByProductToPartId.TryGetValue(
                part.ProductToPartId,
                out var node)
                    ? node
                    : null;
        }
       
       
   private List<TechnologyTreeItem> BuildChildren(WorkflowGraphNode node, int level)
        {
            var result = new List<TechnologyTreeItem>();

            foreach (var next in node.Next)
            {
                var childLevel = level;

                if (next.Node.NodeType == 1)
                    childLevel = level + 1;

                result.Add(CreateNode(next, childLevel));
            }

            return result;
        }
        
        private void BuildBranch(
            WorkflowGraphNode current,
            List<TechnologyTreeItem> items,
            int level)
                {
                    foreach (var next in current.Next.OrderBy(x => x.Node.SortOrder))
                    {
                        var item = CreateNode(next, level);

                        item.Parent = items.LastOrDefault();
                        item.IsFlowChild = true;
                        item.IsLastChild = next == current.Next.Last();

                        items.Add(item);

                        BuildBranch(next, item.NodeChildren, level);
                        
                    }
                }

    private TechnologyTreeItem CreateNode(WorkflowGraphNode node, int level)
        {
            var parentPart = State.ProductParts
                .FirstOrDefault(x => x.AttachToNodeId == node.Node.Id);

            return new TechnologyTreeItem
            {
                Node = node.Node,
                Level = level,
                InputCount = node.Previous.Count,
                PartChildren = new List<TechnologyTreeItem>(),
                NodeChildren = new List<TechnologyTreeItem>(),
                IsFlowChild = true
            };
        }
       
    private TechnologyTreeItem CreatePart(WorkflowPartModel part)
        {
            var node = FindPartNode(part);
               var result = new TechnologyTreeItem
                {
                    Part = part,
                    Node = node?.Node,
                    PartChildren = new List<TechnologyTreeItem>(),
                    NodeChildren = new List<TechnologyTreeItem>(),
                    Level = 0
                };

            if (node != null)
                {
                    BuildBranch(node, result.NodeChildren, 1);
                }

            return result;
        }
    
       public AvailableFlowDto? GetCurrentFlow()
            {
                return State.SelectedFlow;
            }

        public async Task<bool> AddProcessAsync(string processName)
            {
                if (!CanAddProcess())
                    return false;

                if (SelectedNode == null)
                    return false;

                var targetNode = SelectedNode;

                var response = await _workflowApiService.AddProcessAsync(
                    Workflow!.Workflow!.Id,
                    targetNode.Node.Id,
                    processName);

                if (!response.IsSuccessStatusCode)
                    return false;

                var node = await response.Content.ReadFromJsonAsync<WorkflowNodeModel>();

                if (node == null)
                    return false;
                
                // var selectedProductToPartId = SelectedPart!.ProductToPartId;

                var currentPart = GetCurrentFlowOwner();

                    if (currentPart == null)
                        return false;

                var selectedProductToPartId = currentPart.ProductToPartId;

                await ReloadAsync();

                RefreshValidationState();

                RestoreSelectedStructureNode(node.Id);

                return true;
            }

        public async Task<bool> AddMergeAsync(
            int currentFinishNodeId,
            List<int> mergeFinishNodeIds)
            {
                if (Workflow?.Workflow == null)
                    return false;

                var response = await _workflowApiService.AddMergeAsync(
                    Workflow.Workflow.Id,
                    currentFinishNodeId,
                    mergeFinishNodeIds);

                if (!response.IsSuccessStatusCode)
                    return false;

                await ReloadAndRestoreSelectionAsync();

                RefreshValidationState();

                return true;
            }

        private bool IsPartNode(WorkflowGraphNode node)
        {
            return node.Node.NodeType == 1;
        }

        public async Task LoadAvailableTopPartsAsync(int versionId)
            {
                State.AvailableTopParts =
                    await _workflowApiService.LoadAvailableTopPartsAsync(versionId);
            }
        
        public async Task LoadAvailableSubPartsAsync(int versionId)
            {
                State.AvailableTopParts =
                    await _workflowApiService.LoadAvailableSubPartsAsync(versionId);
            }

        public async Task<bool> AddTopPartAsync(
            int topPartId,
            int? parentProductTopPartId,
            int? attachToNodeId)
        {
            if (Workflow?.Workflow == null)
                return false;

            var response = await _workflowApiService.AddTopPartAsync(
                Workflow.Workflow.VersionId,
                topPartId,
                parentProductTopPartId,
                attachToNodeId);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddSubPartAsync(
                int topPartId,
                int parentProductTopPartId,
                int attachToNodeId)
            {
                if (Workflow?.Workflow == null)
                    return false;

                var response = await _workflowApiService.AddSubPartAsync(
                    Workflow.Workflow.VersionId,
                    topPartId,
                    parentProductTopPartId,
                    attachToNodeId);

                return response.IsSuccessStatusCode;
            }

        public void SelectTreeItem(TechnologyTreeItem item)
            {               
                if (State.SelectedTreeItem == item)
                    {
                        item.IsSelected = false;
                        State.SelectedTreeItem = null;
                        State.SelectedNode = null;
                        return;
                    }

                if (item.Part == null)
                    return;

                SelectPart(item.Part);

            }

        public void SelectStructureItem(TechnologyStructureItem item)
            {                
                if (State.SelectedStructureItem == item)
                {
                    item.IsSelected = false;
                    State.SelectedStructureItem = null;
                    State.SelectedFlow = item.Flow;
                    State.SelectedNode = null;
                    return;
                }

                ClearStructureSelection(State.TechnologyStructure);

                item.IsSelected = true;
                State.SelectedStructureItem = item;

                if (item.Flow != null)
                    {
                        State.SelectedFlow = item.Flow;
                    }

                State.SelectedNode = item.Node != null
                    ? Graph.GetValueOrDefault(item.Node.Id)
                    : null;

                _ = LoadAvailableFlowsAsync();
            }

        public string? ValidationMessage
            {
                get
                {
                    if (!SelectedProcessHasValidationError)
                        return null;

                    return "Lai turpinātu, aizpildiet obligātos PROCESS laukus: Nosaukums, Darba centrs un Izpildes laiks.";
                }
            }

        public void SelectPart(WorkflowPartModel part)
            {
                if (part == null)
                    return;
                
                var item = FindTreeItem(
                    State.TechnologyTree,
                    part.ProductToPartId);

                if (item != null)
                {
                    SetSelectedTreeItem(item);
                }
            }

        private TechnologyTreeItem? FindTreeItem(
            IEnumerable<TechnologyTreeItem> items,
            int productToPartId)
        {
            foreach (var item in items)
            {
                if (item.Part?.ProductToPartId == productToPartId)
                    return item;

                var child = FindTreeItem(item.PartChildren, productToPartId);

                if (child != null)
                    return child;
            }

            return null;
        }

        private void SetSelectedTreeItem(TechnologyTreeItem item)
            {
                ClearSelection(State.TechnologyTree);
                item.IsSelected = true;

                State.SelectedTreeItem = item;

                State.SelectedNode = item.Node == null
                    ? null
                    : Graph.GetValueOrDefault(item.Node.Id);
            }
        
        private void ClearSelection(IEnumerable<TechnologyTreeItem> items)
            {
                foreach (var item in items)
                {
                    item.IsSelected = false;
                    ClearSelection(item.PartChildren);
                    ClearSelection(item.NodeChildren);
                }
            }
        
        private void ClearStructureSelection(
            IEnumerable<TechnologyStructureItem> items)
        {
            foreach (var item in items)
            {
                item.IsSelected = false;

                ClearStructureSelection(item.Children);
            }
        }
        
        private void FillFinishNodes(List<MergeFinishItemDto> items)
            {
                foreach (var item in items)
                {
                    State.AvailableFinishNodes.Add(new MergeFinishItem
                    {
                        Id = item.Id,
                        Name = item.Name ?? "",
                        Selected = false
                    });
                }
            }

            public async Task<bool> SaveNodeCommentsAsync()
                {
                    if (SelectedNode == null)
                        return false;

                    var response = await _workflowApiService.SaveNodeCommentsAsync(
                        SelectedNode.Node.Id,
                        SelectedNode.Node.Comments);

                    return response.IsSuccessStatusCode;
                }

            public string? GetCurrentTopPartName()
                {
                    var item = SelectedStructureItem;

                    while (item != null)
                    {
                        if (item.Part != null)
                            return item.Part.TopPartName;

                        item = item.Parent;
                    }

                    return null;
                }

            public WorkflowPartModel? GetCurrentFlowOwner()
                {
                    var item = SelectedStructureItem;

                    while (item != null)
                    {
                        if (item.Part != null)
                            return item.Part;

                        item = item.Parent;
                    }

                    return null;
                }
            
            public string? CurrentPartName =>
                GetCurrentFlowOwner()?.TopPartName;
            
            public string CurrentOwnerDescription
                {
                    get
                    {
                        var part = GetCurrentFlowOwner();

                        if (part == null)
                            return "-";

                        var type = part.ParentProductTopPartId == null
                            ? "TOP PART"
                            : "SUB PART";

                        return $"{type}: {part.TopPartName}";
                    }
                }

            public string PreviousNodeName
                {
                    get
                    {
                        var previous = SelectedNode?.Previous.FirstOrDefault();

                        if (previous == null)
                            return "-";

                        return previous.Node.NodeType == 2
                            ? previous.Node.Name
                            : "-";
                    }
                }

            public async Task<bool> SaveQtyPerProductAsync()
                {
                    if (SelectedPart == null)
                        return false;

                    var response = await _workflowApiService.SaveQtyPerProductAsync(
                        SelectedPart.ProductToPartId,
                        SelectedPart.QtyPerProduct);

                    return response.IsSuccessStatusCode;
                }

            public async Task<bool> SaveProcessAsync()
                {
                    if (SelectedNode?.Node?.NodeType != 2)
                        return false;

                    if (string.IsNullOrWhiteSpace(SelectedNode.Node.Name))
                        return false;

                    if (SelectedNode.Node.WorkCenterId == null)
                        return false;

                    if (SelectedNode.Node.EstimatedMinutes == null)
                        return false;

                    var response = await _workflowApiService.SaveProcessAsync(
                        SelectedNode.Node);

                    RefreshValidationState();

                    return response.IsSuccessStatusCode;
                }

                public bool HasIncompleteProcess =>
                    Graph.Values.Any(x =>
                        x.Node.NodeType == 2 &&
                        (
                            string.IsNullOrWhiteSpace(x.Node.Name) ||
                            x.Node.WorkCenterId == null ||
                            x.Node.EstimatedMinutes == null
                        ));

                public bool IsEditorLocked =>
                    HasIncompleteProcess;

        public void RefreshValidationState()
            {
                foreach (var root in State.TechnologyStructure)
                {
                    RefreshValidation(root);
                }
            }

private void RefreshValidation(TechnologyStructureItem item)
    {
        item.HasProcessValidationError =
        item.Node?.NodeType == 2 &&
        (
            string.IsNullOrWhiteSpace(item.Node.Name) ||
            item.Node.WorkCenterId == null ||
            item.Node.EstimatedMinutes == null
        );

        foreach (var child in item.Children)
        {
            RefreshValidation(child);
        }
    }

    public bool SelectedProcessHasValidationError
        {
            get
            {
                if (SelectedNode?.Node?.NodeType != 2)
                    return false;

                return string.IsNullOrWhiteSpace(SelectedNode.Node.Name)
                    || SelectedNode.Node.WorkCenterId == null
                    || SelectedNode.Node.EstimatedMinutes == null;
            }
        }

    public async Task<bool> AddFinishAsync()
        {
            if (Workflow?.Workflow == null)
                return false;

            if (SelectedNode == null)
                return false;

            var response = await _workflowApiService.AddFinishAsync(
                Workflow.Workflow.Id,
                SelectedNode.Node.Id);

            if (!response.IsSuccessStatusCode)
                return false;

            await ReloadAndRestoreSelectionAsync();

            RefreshValidationState();

            return true;
        }

}