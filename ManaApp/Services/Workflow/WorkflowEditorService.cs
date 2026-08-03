using ManaApp.ViewModels.Workflow;
using ManaApp.Shared.DTOs.Workflow;
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
            _stateService.State.Explorer = state.Explorer;
            _stateService.State.ProductParts = state.ProductParts;
            _stateService.State.WorkCenters =
                await _lookupService.LoadWorkCentersAsync();

            RefreshTechnologyExplorer();

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
        
        public bool CanAddProcess() =>
            State.AvailableActions.CanAddProcess;
        
        public bool CanAddSubPart() =>
            State.AvailableActions.CanAddSubPart;
        
        public bool CanAddTopPart() =>
            State.SelectedWorkflowExplorerItem == null;

        public bool CanAddMerge()
            {
                return SelectedNode != null &&
                    SelectedNode.Node.NodeType is 1 or 3;
            }

        public bool CanAddFinish() =>
            State.AvailableActions.CanAddFinish;
        
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
        public WorkflowGraphItem? SelectedGraphItem => State.SelectedGraphItem;
        public WorkflowPartModel? SelectedPart => State.SelectedGraphItem?.Part;
        public List<WorkflowGraphItem> WorkflowGraphItems { get; set; } = new();
        public List<WorkflowExplorerItemDto> WorkflowExplorerItems => State.Explorer;
        
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

            var selectedExplorer = State.SelectedWorkflowExplorerItem;

            await LoadAsync(Workflow.Workflow.VersionId);

            State.SelectedWorkflowExplorerItem = selectedExplorer;

            await RestoreSelectionAsync();  

        }
    
    private async Task RestoreSelectionAsync()
        {
            var selected = State.SelectedWorkflowExplorerItem;

            if (selected == null)
                return;

            if (TrySelectNode(selected.WorkflowNodeId))
            {
                await LoadAvailableActionsAsync(selected.WorkflowNodeId);
            }
        }

    public async Task ReloadAndRestoreSelectionAsync()
            {
                await ReloadAsync();
            }
    
    public void RefreshTechnologyTree()
        {
            RefreshTechnologyExplorer();
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


       public void RestoreSelectedGraphNode(int nodeId)
            {
                var item = WorkflowGraphItems
                    .Select(root => FindGraphItem(root, nodeId))
                    .FirstOrDefault(x => x != null);

                if (item == null)
                    return;

                foreach (var root in WorkflowGraphItems)
                    {
                        ClearGraphSelection(root);
                    }

                    item.IsSelected = true;

                State.SelectedGraphItem = item;
                State.SelectedNode = Graph.GetValueOrDefault(nodeId);

                if (item.Flow != null)
                    {
                        State.SelectedFlow = item.Flow;
                    }
            }

            private void ClearGraphSelection(WorkflowGraphItem item)
                {
                    item.IsSelected = false;

                    foreach (var child in item.Children)
                    {
                        ClearGraphSelection(child);
                    }
                }

private WorkflowGraphItem? FindGraphItem(WorkflowGraphItem? item, int nodeId)
{
    if (item == null)
        return null;

    if (item.Node?.Id == nodeId)
        return item;

    foreach (var child in item.Children)
    {
        var found = FindGraphItem(child, nodeId);

        if (found != null)
            return found;
    }

    foreach (var next in item.NextNodes)
    {
        var found = FindGraphItem(next, nodeId);

        if (found != null)
            return found;
    }

    return null;
}

    public void RestoreSelectedGraphItem(int productToPartId)
            {
                foreach (var root in WorkflowGraphItems)
                {
                    var item = FindGraphPart(root, productToPartId);

                    if (item != null)
                    {
                        SelectGraphItem(item);
                        return;
                    }
                }
            }
    
    private WorkflowGraphItem? FindGraphPart(WorkflowGraphItem item, int productToPartId)
            {
                if (item.Part?.ProductToPartId == productToPartId)
                    return item;

                foreach (var child in item.Children)
                {
                    var found = FindGraphPart(child, productToPartId);

                    if (found != null)
                        return found;
                }

                foreach (var next in item.NextNodes)
                {
                    var found = FindGraphPart(next, productToPartId);

                    if (found != null)
                        return found;
                }

                return null;
            }

    
    private IEnumerable<WorkflowPartModel> GetAttachedParts(int nodeId)
        {
            return State.ProductParts
                .Where(x => x.AttachToNodeId == nodeId)
                .OrderBy(x => x.TopPartName);
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
       
   
       public AvailableFlowDto? GetCurrentFlow()
            {
                return State.SelectedFlow;
            }

        public async Task<WorkflowNodeModel?> AddProcessAsync(string processName)
            {
                if (!CanAddProcess())
                    return null;

                if (SelectedNode == null)
                    return null;

                var selectedNode = SelectedNode;

                var response = await _workflowApiService.AddProcessAsync(
                    Workflow!.Workflow!.Id,
                    selectedNode.Node.Id,
                    processName);

                if (!response.IsSuccessStatusCode)
                    return null;

                var node = await response.Content.ReadFromJsonAsync<WorkflowNodeModel>();

                if (node == null)
                    return null;
                

                await ReloadAsync();

                RefreshValidationState();

                RestoreSelectedGraphNode(node.Id);

                await LoadAvailableActionsAsync(node.Id);

                return node;
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

            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content
                .ReadFromJsonAsync<AddTopPartResponse>();

            if (result == null)
                return false;

            await ReloadAsync();

            return true;

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

                if (response == null)
                    return false;
                
                await ReloadAsync();

                State.SelectedWorkflowExplorerItem = new WorkflowExplorerItemDto
                    {
                        WorkflowNodeId = response.WorkflowNodeId
                    };

                    await ReloadAsync();

                    return true;

            }

        public void SelectGraphItem(WorkflowGraphItem item)
            {
                
 Console.WriteLine(
    $"SelectGraphItem: current={State.SelectedGraphItem?.Node?.Id}, clicked={item.Node?.Id}");

Console.WriteLine($"ReferenceEquals = {ReferenceEquals(State.SelectedGraphItem, item)}");

                if (State.SelectedGraphItem == item)
                {
                    item.IsSelected = false;
                    State.SelectedGraphItem = null;
                    State.SelectedFlow = item.Flow;
                    State.SelectedNode = null;
                    return;
                }

                State.SelectedGraphItem = item;
                item.IsSelected = true;

                if (item.Flow != null)
                    State.SelectedFlow = item.Flow;

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

                RestoreSelectedGraphItem(part.ProductToPartId);
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
Console.WriteLine($"SelectedNodeId = {State.SelectedNode?.Node.Id}");

foreach (var p in State.ProductParts)
{
    Console.WriteLine(
        $"Part: Node={p.WorkflowNodeId}, ProductToPartId={p.ProductToPartId}, Name={p.TopPartName}");
}
                   
                    var nodeId = State.SelectedNode?.Node.Id;

                    if (nodeId == null)
                        return null;

                    return State.ProductParts
                        .FirstOrDefault(x => x.WorkflowNodeId == nodeId)?
                        .TopPartName;
                }

            public WorkflowPartModel? GetCurrentFlowOwner()
                {
                    var nodeId = State.SelectedNode?.Node.Id;

                    if (nodeId == null)
                        return null;

                    return State.ProductParts
                        .FirstOrDefault(x => x.WorkflowNodeId == nodeId);
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

                    var node = await _workflowApiService.SaveProcessAsync(
                        SelectedNode.Node);

                    if (node == null)
                        return false;

                    SelectedNode.Node.Name = node.Name;
                    SelectedNode.Node.WorkCenterId = node.WorkCenterId;
                    SelectedNode.Node.EstimatedMinutes = node.EstimatedMinutes;
                    SelectedNode.Node.Comments = node.Comments;
                    UpdateExplorerNodeName(node.Id, node.Name);

                    RefreshValidationState();

                    await LoadAvailableActionsAsync(SelectedNode.Node.Id);
                    

                    return true;
                }


                private void UpdateExplorerNodeName(int workflowNodeId, string name)
                    {
                        foreach (var item in State.Explorer)
                            UpdateExplorerNodeName(item, workflowNodeId, name);
                    }

                    private void UpdateExplorerNodeName(
                        WorkflowExplorerItemDto item,
                        int workflowNodeId,
                        string name)
                    {
                        var node = item.Nodes.FirstOrDefault(x => x.WorkflowNodeId == workflowNodeId);

                        if (node != null)
                        {
                            node.Name = name;
                            return;
                        }

                        foreach (var child in item.Children)
                            UpdateExplorerNodeName(child, workflowNodeId, name);
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
    foreach (var root in WorkflowGraphItems)
    {
        RefreshValidation(root);
    }
}

private void RefreshValidation(WorkflowGraphItem item)
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

        public async Task LoadAvailableActionsAsync(int selectedNodeId)
            {
                if (Workflow?.Workflow == null)
                    return;

                State.AvailableActions =
                    await _workflowApiService.LoadAvailableActionsAsync(
                        Workflow.Workflow.Id,
                        selectedNodeId);

            }

}