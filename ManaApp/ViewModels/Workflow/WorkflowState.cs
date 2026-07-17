using ManaApp.Models;
using ManaApp.DTOs.Workflow;
using ManaApp.Models.Lookup;


namespace ManaApp.ViewModels.Workflow;

public class WorkflowState
{
    public WorkflowDto? Workflow { get; set; }

    public Dictionary<int, WorkflowGraphNode> Graph { get; set; } = new();
    public Dictionary<int, WorkflowGraphNode> PartNodeByProductToPartId { get; set; } = new();
    public WorkflowGraphNode? SelectedNode { get; set; }
    public List<MergeFinishItem> AvailableFinishNodes { get; set; } = new();
    public TechnologyTreeItem? SelectedTreeItem { get; set; }
    public List<WorkflowTopPartSelectDto> AvailableTopParts { get; set; } = new();
    public List<LookupItem> WorkCenters { get; set; } = new();
    public List<TechnologyExplorerItem> TechnologyExplorer { get; set; } = new();
    public List<MergeFlowItem> AvailableFlows { get; set; } = new();

    public TechnologyExplorerItem? SelectedExplorerItem { get; set; }

    public List<WorkflowPartModel> ProductParts { get; set; } = new();
    public List<TechnologyTreeItem> TechnologyTree { get; set; } = new();
    public List<TechnologyStructureItem> TechnologyStructure { get; set; } = new();
    public TechnologyStructureItem? SelectedStructureItem { get; set; }
    public int SelectedTopPartId { get; set; }
}