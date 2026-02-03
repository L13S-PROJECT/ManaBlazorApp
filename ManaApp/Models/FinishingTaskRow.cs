namespace ManaApp.Models;

public sealed class FinishingTaskRow
{
    public int TaskId { get; set; }
    public string? PartName { get; set; }
    public int Status { get; set; }
    public int? Assigned_To { get; set; }
    public int? Claimed_By { get; set; }
    public int Done { get; set; }
    public int TopPartStepId { get; set; }
    public int ProductToPartId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Comment { get; set; }
    public bool IsCommentForEmployee { get; set; }

}
