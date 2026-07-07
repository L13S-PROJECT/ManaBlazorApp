namespace ManaApp.Models;

public class TechnologyTreeItem
{
    public WorkflowPartModel Part { get; set; } = null!;

    public WorkflowNodeModel? Node { get; set; }

    public List<TechnologyTreeItem> Children { get; set; } = new();

    public int? NodeId => Node?.Id;

    public bool IsActive => Node != null;
    public bool IsPart => Part != null;
    public bool IsProcess => Node?.NodeType == 2;

    public int Level { get; set; }

}