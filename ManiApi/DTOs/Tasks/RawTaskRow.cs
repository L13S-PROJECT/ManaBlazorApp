namespace ManiApi.DTOs.Tasks
{
    
public sealed class RawTaskRow
{
    public int TaskId { get; set; }
    public int BatchProductId { get; set; }
    public int TopPartStepId { get; set; }
    public int Status { get; set; }
    public int? Assigned_To { get; set; }
    public int? Claimed_By { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int Qty { get; set; }
    public string? StepName { get; set; }
    public string? TopPartName { get; set; }
    public int ProductToPartId { get; set; }
    public string? Comment { get; set; }
    public bool IsCommentForEmployee { get; set; }
}

}