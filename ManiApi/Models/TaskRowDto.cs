namespace ManiApi.Models;

public sealed class TaskRowDto
{
    public int TaskId { get; set; }
    public byte Priority { get; set; }
    public bool BatchPriority { get; set; }
    public int ProductToPartId { get; set; }
    public int Status { get; set; }
    public int PriorityLevel { get; set; }
    public bool Tasks_Push { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public bool IsCommentForEmployee { get; set; }
    public string? Comment { get; set; }

    public string? ProductName { get; set; }
    public string? PartName { get; set; }
    public string? StepName { get; set; }
    public int RootId { get; set; }
    public int EstimatedMinutes { get; set; }
    public int ActualMinutes { get; set; }
    public int EstimatedTotalMinutes { get; set; }
    public int EstimatedStartMinutes { get; set; }
    public bool CanStart { get; set; }
    public string? BatchCode { get; set; }

    public int Done { get; set; }
    public int? Assigned_To { get; set; }
    public int Planned { get; set; }

    public int StepOrder { get; set; }

    public int StepType { get; set; }
    public int BatchId { get; set; }
    public int VersionId { get; set; }
    public int BatchProductId { get; set; }
}