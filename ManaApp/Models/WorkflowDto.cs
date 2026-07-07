namespace ManaApp.Models;

public class WorkflowDto
{
    public WorkflowModel? Workflow { get; set; }

    public List<WorkflowNodeModel> Nodes { get; set; } = new();

    public List<WorkflowConnectionModel> Connections { get; set; } = new();
    public List<WorkflowPartModel> ProductParts { get; set; } = new();
}

public class WorkflowModel
{
    public int Id { get; set; }

    public int VersionId { get; set; }

    public string Name { get; set; } = "";
}

public class WorkflowNodeModel
{
    public int Id { get; set; }

    public int WorkflowId { get; set; }

    public int NodeType { get; set; }

    public string Name { get; set; } = "";
    public int? TopPartId { get; set; }

    public int? ProductToPartId { get; set; }

    public int? WorkCenterId { get; set; }

    public int SortOrder { get; set; }
}

public class WorkflowConnectionModel
{
    public int Id { get; set; }

    public int FromNodeId { get; set; }

    public int ToNodeId { get; set; }
}