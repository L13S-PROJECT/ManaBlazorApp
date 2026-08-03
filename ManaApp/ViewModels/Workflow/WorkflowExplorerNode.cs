namespace ManaApp.ViewModels.Workflow;

public class WorkflowExplorerNode
{
    public WorkflowGraphItem Item { get; set; } = null!;

    public List<WorkflowExplorerNode> Dependencies { get; set; } = new();

    public int Depth { get; set; }

    public bool IsDependency { get; set; }
}