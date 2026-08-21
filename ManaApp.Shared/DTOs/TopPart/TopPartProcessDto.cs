namespace ManaApp.Shared.DTOs.TopPart;

public class CreateTopPartProcessRequest
{
    public int WorkflowId { get; set; }
    public List<int> SelectedNodeIds { get; set; } = new();
    public string ProcessName { get; set; } = "";
    public int WorkCenterId { get; set; }
    public int EstimatedMinutes { get; set; }
    public uint? StepTypeId { get; set; }

    public string WipName { get; set; } = "";
}

public class UpdateTopPartProcessRequest
{
    public int WorkflowId { get; set; }
    public int ProcessNodeId { get; set; }
    public string ProcessName { get; set; } = "";
    public string WipName { get; set; } = "";
    public int WorkCenterId { get; set; }
    public uint? StepTypeId { get; set; }
    public int EstimatedMinutes { get; set; }
}