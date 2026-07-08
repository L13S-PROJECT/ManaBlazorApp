using ManaApp.ViewModels.Workflow;
using ManaApp.DTOs.Workflow;
using ManaApp.Models;
using System.Net.Http.Json;

namespace ManaApp.Services.Workflow;

public class WorkflowEditorService
{
    private readonly WorkflowApiService _workflowApiService;
    private readonly WorkflowStateService _stateService;

    public WorkflowEditorService(
        WorkflowApiService workflowApiService,
        WorkflowStateService stateService)
    {
        _workflowApiService = workflowApiService;
        _stateService = stateService;
    }

    public async Task<WorkflowState?> LoadAsync(int versionId)
        {
            _stateService.Clear();

            var state = await _workflowApiService.LoadStateAsync(versionId);

            if (state == null)
                return null;

            _stateService.State.Workflow = state.Workflow;
            _stateService.State.Graph = state.Graph;
            _stateService.State.PartNodeByProductToPartId = state.PartNodeByProductToPartId;
            _stateService.State.SelectedNode = state.SelectedNode;
            _stateService.State.AvailableFinishNodes = state.AvailableFinishNodes;
            _stateService.State.ProductParts = state.ProductParts;
            RefreshTechnologyTree();

            return _stateService.State;
        }

        public bool TrySelectNode(int nodeId)
            {
                if (!_workflowApiService.TrySelectNode(
                    _stateService.State.Graph,
                    nodeId,
                    out var node))
                {
                    return false;
                }

                _stateService.State.SelectedNode = node;
                return true;
            }
        
        public bool CanAddProcess()
            {
                return SelectedTreeItem?.Part != null;
            }

        public WorkflowState State => _stateService.Current;
        public WorkflowDto? Workflow => State.Workflow;
        public Dictionary<int, WorkflowGraphNode> Graph => State.Graph;
        public WorkflowGraphNode? SelectedNode => State.SelectedNode;
        public TechnologyTreeItem? SelectedTreeItem => State.SelectedTreeItem;
        public WorkflowPartModel? SelectedPart => SelectedTreeItem?.Part;

        public async Task<List<MergeFinishItemDto>?> LoadFinishNodesAsync(int workflowId)
            {
                return await _workflowApiService.LoadFinishNodesAsync(workflowId);
            }
        
        public List<MergeFinishItem> AvailableFinishNodes =>
            _stateService.Current.AvailableFinishNodes;

        public void ClearFinishNodes()
            {
                State.AvailableFinishNodes.Clear();
            }
        
        public async Task SelectNodeAsync(int nodeId)
            {
                if (!TrySelectNode(nodeId))
                    return;

                ClearFinishNodes();

                if (State.SelectedNode?.Node.NodeType == 3)
                {
                    var items = await LoadFinishNodesAsync(State.Workflow!.Workflow!.Id);

                    if (items == null)
                        return;

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
            }
        
        public async Task LoadAvailableFinishNodesAsync()
            {
                if (State.Workflow?.Workflow == null)
                    return;

                var items = await LoadFinishNodesAsync(State.Workflow.Workflow.Id);

                ClearFinishNodes();

                if (items == null)
                    return;

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
        
    public async Task<List<WorkflowPartModel>> LoadProductPartsAsync(int versionId)
        {
            return await _workflowApiService.LoadProductPartsAsync(versionId);
        }

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

    public async Task<HttpResponseMessage> CreateNodeAsync(CreateWorkflowNodeRequest request)
        {
            return await _workflowApiService.CreateNodeAsync(request);
        }
    
    public async Task<HttpResponseMessage> CreateConnectionAsync(CreateWorkflowConnectionRequest request)
        {
            return await _workflowApiService.CreateConnectionAsync(request);
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

            await ReloadAsync();

            if (selectedPartId.HasValue)
            {
                RestoreSelectedTreeItem(selectedPartId.Value);
            }
        }
    
    public void RefreshTechnologyTree()
        {
            State.TechnologyTree = BuildTechnologyTree();
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

    private List<TechnologyTreeItem> BuildTechnologyTree()
            {
                var roots = State.ProductParts
                    .Where(x => x.ParentProductTopPartId == null)
                    .OrderBy(x => x.TopPartName);

                var result = new List<TechnologyTreeItem>();

                foreach (var part in roots)
                {
                    var item = CreatePart(part);

                    BuildPartChildren(item);

                    result.Add(item);
                }

                return result;
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

                    item.IsLastChild = i == list.Count - 1;

                    parent.Children.Add(item);

                    BuildPartChildren(item);
                }
        }

    private List<TechnologyTreeItem> NodeChildren(WorkflowGraphNode? node)
            {
                return node == null
                    ? new()
                    : BuildChildren(node, 1);
            }
    
    private WorkflowGraphNode? FindPartNode(WorkflowPartModel part)
        {
            if (part == null)
                return null;

            return State.PartNodeByProductToPartId.TryGetValue(
                part.ProductToPartId,
                out var node)
                    ? node
                    : null;
        }
    
    private async Task<WorkflowGraphNode?> GetOrCreateSelectedPartNodeAsync()
        {
            if (SelectedPart == null)
                return null;

            var node = FindPartNode(SelectedPart);

            if (node != null)
                return node;

            var created = await _workflowApiService.CreatePartNodeAsync(
                Workflow!.Workflow!.Id,
                SelectedPart);

            if (created == null)
                return null;

            await ReloadAsync();

            return FindPartNode(SelectedPart);
        }
    
    private WorkflowGraphNode FindLastNode(WorkflowGraphNode node)
        {
            while (node.Next.Count > 0)
            {
                node = node.Next[0];
            }

            return node;
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
            var nextNodes = current.Next.ToList();

            for (int i = 0; i < nextNodes.Count; i++)
            {
                var next = nextNodes[i];

                if (IsPartNode(next))
                {
                    var partItem = CreateNode(next, level + 1);

                    partItem.IsLastChild = i == nextNodes.Count - 1;

                    items.Add(partItem);

                    BuildBranch(next, partItem.Children, level + 1);

                    continue;
                }

                var nodeItem = CreateNode(next, level);

                nodeItem.IsLastChild = i == nextNodes.Count - 1;

                items.Add(nodeItem);

                BuildBranch(next, items, level);
            }
        }

    private TechnologyTreeItem CreateNode(WorkflowGraphNode node, int level)
        {
            return new TechnologyTreeItem
            {
                Node = node.Node,
                Level = level,
                InputCount = node.Previous.Count,
                Children = new List<TechnologyTreeItem>()
            };
        }
       
    private TechnologyTreeItem CreatePart(WorkflowPartModel part)
        {
            var node = FindPartNode(part);

Console.WriteLine(
    $"PartId={part.ProductToPartId}, Node={(node == null ? "NULL" : node.Node.Id)}");

            var result = new TechnologyTreeItem
                {
                    Part = part,
                    Node = node?.Node,
                    Children = new List<TechnologyTreeItem>(),
                    Level = 0
                };

            if (node != null)
                {
                    BuildBranch(node, result.Children, 1);
                }

            return result;
        }
    
    public async Task<WorkflowNodeModel?> CreateProcessAsync(
    int previousNodeId,
    string processName)
        {
            var response = await CreateNodeAsync(new CreateWorkflowNodeRequest
            {
                WorkflowId = Workflow!.Workflow!.Id,
                NodeType = 2,
                Name = processName
            });

            if (!response.IsSuccessStatusCode)
                return null;

            var node = await response.Content.ReadFromJsonAsync<WorkflowNodeModel>();

            if (node == null)
                return null;
            
            var connectResponse = await CreateConnectionAsync(
                new CreateWorkflowConnectionRequest
                {
                    FromNodeId = FindLastNode(
                            Graph[previousNodeId]
                        ).Node.Id,
                    ToNodeId = node.Id
                });

            // if (!connectResponse.IsSuccessStatusCode)
            //     return null;

            if (!connectResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine(await connectResponse.Content.ReadAsStringAsync());
                    return null;
                }

            return node;
        }

        public async Task<bool> AddProcessAsync(string processName)
            {
                if (!CanAddProcess())
                    return false;

                var partNode = await GetOrCreateSelectedPartNodeAsync();

                    if (partNode == null)
                        return false;

                var node = await CreateProcessAsync(
                     partNode.Node.Id,
                    processName);

                if (node == null)
                    return false;
                
                var selectedProductToPartId = SelectedPart!.ProductToPartId;

                await ReloadAsync();

                RestoreSelectedTreeItem(selectedProductToPartId);

                await SelectNodeAsync(node.Id);

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
            int? parentProductTopPartId)
        {
            if (Workflow?.Workflow == null)
                return false;

            var response = await _workflowApiService.AddTopPartAsync(
                Workflow.Workflow.VersionId,
                topPartId,
                parentProductTopPartId);

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

                var child = FindTreeItem(item.Children, productToPartId);

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
                    ClearSelection(item.Children);
                }
            }

}