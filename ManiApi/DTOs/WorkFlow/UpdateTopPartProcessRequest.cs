namespace ManiApi.DTOs.WorkFlow;

public class UpdateTopPartProcessRequest
{
    public int WorkflowId { get; set; }

    public int ProcessNodeId { get; set; }

    public string ProcessName { get; set; } = "";

    public int WorkCenterId { get; set; }

    public int EstimatedMinutes { get; set; }
}