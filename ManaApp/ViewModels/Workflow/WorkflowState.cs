using ManaApp.Models;
using ManaApp.Models.Lookup;
using ManaApp.Shared.DTOs.Workflow;


namespace ManaApp.ViewModels.Workflow;

public class WorkflowState
{
    public WorkflowDto? Workflow { get; set; }
    public Dictionary<int, WorkflowGraphNode> Graph { get; set; } = new();
    public Dictionary<int, WorkflowGraphNode> PartNodeByProductToPartId { get; set; } = new();
    public WorkflowGraphNode? SelectedNode { get; set; }
    public List<MergeFinishItem> AvailableFinishNodes { get; set; } = new();
    public AvailableFlowDto? SelectedFlow { get; set; }
    public bool CanMergeCurrentFlow { get; set; }
    public List<WorkflowTopPartSelectDto> AvailableTopParts { get; set; } = new();
    public List<LookupItem> WorkCenters { get; set; } = new();
    public List<TechnologyExplorerItem> TechnologyExplorer { get; set; } = new();
    public List<MergeFlowItem> AvailableFlows { get; set; } = new();
    public List<int> InvalidFlowOwnerNodeIds { get; set; } = new();
    public TechnologyExplorerItem? SelectedExplorerItem { get; set; }
    public WorkflowGraphItem? SelectedGraphItem { get; set; }
    public List<WorkflowPartModel> ProductParts { get; set; } = new();
    public int SelectedTopPartId { get; set; }
    public WorkflowActionsDto AvailableActions { get; set; } = new();
    public List<WorkflowExplorerItemDto> Explorer { get; set; } = new();
    public WorkflowExplorerItemDto? SelectedWorkflowExplorerItem { get; set; }
}