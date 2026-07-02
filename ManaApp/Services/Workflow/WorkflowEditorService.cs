using ManaApp.ViewModels.Workflow;
using ManaApp.DTOs.Workflow;
using ManaApp.Models;

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
            var state = await _workflowApiService.LoadStateAsync(versionId);

            if (state == null)
                return null;

            _stateService.State.Workflow = state.Workflow;
            _stateService.State.Graph = state.Graph;
            _stateService.State.SelectedNode = state.SelectedNode;
            _stateService.State.AvailableFinishNodes = state.AvailableFinishNodes;

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

        public WorkflowState State => _stateService.Current;
        public WorkflowDto? Workflow => State.Workflow;
        public Dictionary<int, WorkflowGraphNode> Graph => State.Graph;
        public WorkflowGraphNode? SelectedNode => State.SelectedNode;

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

}