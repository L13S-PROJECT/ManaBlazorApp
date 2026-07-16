namespace ManiApi.DTOs.WorkFlow;

public class SaveProcessRequest
{
    public int NodeId { get; set; }

    public string Name { get; set; } = "";

    public int? WorkCenterId { get; set; }

    public int? EstimatedMinutes { get; set; }

    public string? Comments { get; set; }
}