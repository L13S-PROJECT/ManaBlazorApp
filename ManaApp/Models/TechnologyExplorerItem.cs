namespace ManaApp.Models;

public class TechnologyExplorerItem
{
    public WorkflowPartModel? Part { get; set; }

    public WorkflowGraphNode? GraphNode { get; set; }

    public List<TechnologyExplorerItem> Children { get; set; } = new();

    public TechnologyExplorerItem? Parent { get; set; }

    public bool IsSelected { get; set; }
}