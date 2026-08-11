namespace ManiApi.DTOs.WorkFlow;

public class CreateTopPartProcessRequest
{
    public int WorkflowId { get; set; }

    public int SelectedNodeId { get; set; }

    public string ProcessName { get; set; } = "";

    public int WorkCenterId { get; set; }

    public int EstimatedMinutes { get; set; }
}