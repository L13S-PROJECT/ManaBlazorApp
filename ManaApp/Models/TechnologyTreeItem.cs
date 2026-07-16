namespace ManaApp.Models;

public class TechnologyTreeItem
{
    public WorkflowPartModel Part { get; set; } = null!;

    public WorkflowNodeModel? Node { get; set; }

    public bool HasPart => Part is not null;

        public bool HasNode => Node is not null;

        public WorkflowPartModel PartOrThrow => Part!;

        public WorkflowNodeModel NodeOrThrow => Node!;

    // public List<TechnologyTreeItem> Children { get; set; } = new();
    public List<TechnologyTreeItem> PartChildren { get; set; } = new();
    public List<TechnologyTreeItem> NodeChildren { get; set; } = new();
    public bool IsLastChild { get; set; }
    public int InputCount { get; set; }
    public int? NodeId => Node?.Id;
    public bool IsSelected { get; set; }
    public bool IsActive => Node != null;
    public bool IsPart => Part != null;
    public bool IsProcess => Node?.NodeType == 2;

    public int Level { get; set; }
    public TechnologyTreeItem? Parent { get; set; }

    public bool IsFlowChild { get; set; }

    public bool IsHierarchyChild { get; set; }

}