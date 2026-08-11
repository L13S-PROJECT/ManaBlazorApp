namespace ManiApi.DTOs.WorkFlow;

public class SaveTopPartWorkflowNodeRequest
{
    public int Id { get; set; }

    public byte NodeType { get; set; }

    public string? Name { get; set; }

    public uint? TopPartId { get; set; }

    public int? WorkCenterId { get; set; }

    public int? EstimatedMinutes { get; set; }

    public string? Comments { get; set; }

    public int SortOrder { get; set; }

    public List<SaveTopPartWorkflowProcessComponentRequest> ProcessComponents { get; set; } = new();
}