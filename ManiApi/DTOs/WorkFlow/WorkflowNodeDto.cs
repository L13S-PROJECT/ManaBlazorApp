namespace ManiApi.DTOs.WorkFlow;

public class WorkflowNodeDto
{
    public int Id { get; set; }

    public int WorkflowId { get; set; }

    public byte NodeType { get; set; }

    public string? Name { get; set; }

    public int? ProductToPartId { get; set; }

    public int? WorkCenterId { get; set; }

    public int? EstimatedMinutes { get; set; }

    public string? Comments { get; set; }

    public int SortOrder { get; set; }
}