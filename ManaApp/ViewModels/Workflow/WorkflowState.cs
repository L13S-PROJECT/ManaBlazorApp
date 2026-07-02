using ManaApp.Models;

namespace ManaApp.ViewModels.Workflow;

public class WorkflowState
{
    public WorkflowDto? Workflow { get; set; }

    public Dictionary<int, WorkflowGraphNode> Graph { get; set; } = new();

    public WorkflowGraphNode? SelectedNode { get; set; }
    public List<MergeFinishItem> AvailableFinishNodes { get; set; } = new();
}