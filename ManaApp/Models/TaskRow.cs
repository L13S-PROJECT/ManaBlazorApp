namespace ManaApp.Models;

public sealed class TaskRow
{
    public string? WorkCenter { get; set; }
    
    public int? WorkCenterSort { get; set; }
    public int TaskId { get; set; }
    public byte Priority { get; set; }
    public int Status { get; set; }
    public int PriorityLevel { get; set; }
    public bool Tasks_Push { get; set; }
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
    public int RootId { get; set; }
    public bool IsChild { get; set; }
    public bool HasParent { get; set; }
    public string? Comment { get; set; }
    public bool IsCommentForEmployee { get; set; }
    public bool CanStart { get; set; }
    public bool BatchPriority { get; set; }
    public int? Assigned_To { get; set; }
    public int? DisplayGroupId { get; set; }
    public int? Claimed_By { get; set; }
    public int? RalColorId { get; set; }
    public string? RalColorCode { get; set; }
    public int? WorkCenterId { get; set; }
}
