namespace ManaApp.Models;

public class WorkflowGraphNode
{
    public WorkflowNodeModel Node { get; set; } = null!;

    public List<WorkflowGraphNode> Next { get; set; } = new();

    public List<WorkflowGraphNode> Previous { get; set; } = new();
}