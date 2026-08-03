using ManaApp.Shared.DTOs.Workflow;

namespace ManaApp.Models;

public class WorkflowGraphNode
{
    public WorkflowNodeModel Node { get; set; } = null!;

    public List<WorkflowGraphNode> Next { get; set; } = new();

    public List<WorkflowGraphNode> Previous { get; set; } = new();
    public WorkflowGraphNode? GraphNode { get; set; }
}