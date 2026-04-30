namespace ManiApi.DTOs.Tasks
{
  public class EmployeeTaskRowV2
{
    public int RootId { get; set; }
    public int BatchProductId { get; set; }    
    public bool BatchPriority { get; set; }
    public int ProductToPartId { get; set; }    
    public string? BatchCode { get; set; }
    public string? ProductName { get; set; }
    public string? TopPartName { get; set; }
    public string? StepName { get; set; }
    public string? QtyBreakdown { get; set; }
    public int DisplayQty { get; set; }
    public int DisplayMinutes { get; set; }
    public int TopPartStepId { get; set; }
    public int Status { get; set; }
    public bool? CanStart { get; set; }

    public int? Assigned_To { get; set; }
    public bool Tasks_Priority { get; set; }
    public bool Tasks_Push { get; set; }

    // 🔥 jaunais
    public string RowType { get; set; } = ""; // Parent / ParentChildMerged / SingleChild
    public bool ShowChildMark { get; set; }   // priekš "*"
}  

}