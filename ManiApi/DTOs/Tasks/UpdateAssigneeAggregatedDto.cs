namespace ManiApi.DTOs.Tasks
{
  public sealed class UpdateAssigneeAggregatedDto
{
    public int BatchProductId { get; set; }
    public int TopPartStepId { get; set; }
    public string? RowType { get; set; }
    public int ProductToPartId { get; set; }
    public int? Assigned_To { get; set; }
}  
}