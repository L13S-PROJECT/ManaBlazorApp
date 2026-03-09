namespace ManaApp.Models;

public class GanttRow
{
    public int TaskId { get; set; }
    public int Status { get; set; }
    public int BatchProductId { get; set; }

    public int StepOrder { get; set; }
    public string? StepName { get; set; }
    public int StepType { get; set; }

    public string? PartName { get; set; }

    public int? WorkCenterId { get; set; }
    public string? WorkCenterName { get; set; }

    public int? AssignedTo { get; set; }
    public string? EmployeeName { get; set; }

    public int EstimatedStartMinutes { get; set; }
    public int EstimatedTotalMinutes { get; set; }
    public int ActualMinutes { get; set; }
}