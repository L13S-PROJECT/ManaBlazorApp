namespace ManaApp.Shared.DTOs.TopPart;

public class TopPartWorkflowDto
{
    public TopPartWorkflowModel Workflow { get; set; } = new();
    public List<TopPartWorkflowNodeDto> Nodes { get; set; } = new();
    public List<TopPartWorkflowConnectionDto> Connections { get; set; } = new();
    public List<TopPartWorkflowComponentDto> Components { get; set; } = new();
    public List<TopPartWorkflowProcessComponentDto> ProcessComponents { get; set; } = new();
}

public class TopPartWorkflowModel
{
    public int Id { get; set; }
    public uint? TopPartId { get; set; }
    public int WorkflowVersion { get; set; }
    public int Status { get; set; }
    public bool IsCurrent { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class TopPartWorkflowNodeDto
{
    public int Id { get; set; }
    public int WorkflowId { get; set; }
    public byte NodeType { get; set; }
    public string? Name { get; set; }
    public int? OutputWipNodeId { get; set; }
    public string? OutputWipName { get; set; }
    public uint? TopPartId { get; set; }
    public int? WorkCenterId { get; set; }
    public int? EstimatedMinutes { get; set; }
    public string? Comments { get; set; }
    public int SortOrder { get; set; }
}

public class TopPartWorkflowConnectionDto
{
    public int Id { get; set; }
    public int FromNodeId { get; set; }
    public int ToNodeId { get; set; }
}

public class TopPartWorkflowVersionDto
{
    public int Id { get; set; }
    public uint? TopPartId { get; set; }
    public int WorkflowVersion { get; set; }
    public int Status { get; set; }
    public bool IsCurrent { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class CreateTopPartWorkflowRequest
    {
        public uint TopPartId { get; set; }
    }

public class ReleaseTopPartWorkflowRequest
    {
        public string Description { get; set; } = string.Empty;
    }

public class TopPartWorkflowComponentDto
{
    public int Id { get; set; }
    public int WorkflowId { get; set; }
    public byte ComponentType { get; set; }
    public uint? TopPartId { get; set; }
    public int? ItemId { get; set; }
    public int? ReferencedWorkflowId { get; set; }
    public decimal Quantity { get; set; }
}

public class TopPartWorkflowProcessComponentDto
{
    public int ProcessNodeId { get; set; }
    public int WorkflowComponentId { get; set; }
    public decimal Quantity { get; set; }
}

