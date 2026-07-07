using ManaApp.Models;
using ManaApp.DTOs.Workflow;

namespace ManaApp.ViewModels.Workflow;

public class WorkflowState
{
    public WorkflowDto? Workflow { get; set; }

    public Dictionary<int, WorkflowGraphNode> Graph { get; set; } = new();

    public WorkflowGraphNode? SelectedNode { get; set; }
    public List<MergeFinishItem> AvailableFinishNodes { get; set; } = new();
    public TechnologyTreeItem? SelectedTreeItem { get; set; }
    public List<WorkflowTopPartSelectDto> AvailableTopParts { get; set; } = new();

    public List<WorkflowPartModel> ProductParts { get; set; } = new();
    public List<TechnologyTreeItem> TechnologyTree { get; set; } = new();

    public int SelectedTopPartId { get; set; }
}