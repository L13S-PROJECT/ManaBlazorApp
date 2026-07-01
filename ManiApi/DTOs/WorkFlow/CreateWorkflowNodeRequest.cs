namespace ManiApi.DTOs.WorkFlow;

public class CreateWorkflowNodeRequest
{
    public int WorkflowId { get; set; }

    public byte NodeType { get; set; }

    public string? Name { get; set; }

    public int? TopPartId { get; set; }

    public int? WorkCenterId { get; set; }

    public int? EstimatedMinutes { get; set; }

    public string? Comments { get; set; }
}