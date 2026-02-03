namespace ManaApp.Models;

public sealed class TaskRow
{
    public int TaskId { get; set; }
    public byte Priority { get; set; }
    public int Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? PartName { get; set; }
    public string? StepName { get; set; }
    public string? ProductName { get; set; }
    public string? BatchCode { get; set; }
    public int Planned { get; set; }
    public int Done { get; set; }
    public int StepOrder { get; set; }

    public int StepType { get; set; }
    public int BatchId { get; set; }
    public int VersionId { get; set; }
    public int BatchProductId { get; set; }

    public string? Comment { get; set; }
    public bool IsCommentForEmployee { get; set; }
}
