namespace ManiApi.DTOs.Tasks
{
   public sealed class UnassignedTaskV2Dto
{
    public string? WorkCenter { get; set; }
    public int? WorkCenterSort { get; set; }

    public int TaskId { get; set; }
    public int RootId { get; set; }
    public int TopPartStepId { get; set; }
    public int BatchProductId { get; set; }
    public int ProductToPartId { get; set; }

    public string? BatchCode { get; set; }
    public string? ProductName { get; set; }
    public string? TopPartName { get; set; }
    public string? StepName { get; set; }

    public int Qty { get; set; }
    public string? QtyBreakdown { get; set; } // "x+y"
    public int EstimatedMinutes { get; set; }

    public int Status { get; set; }
    public bool CanStart { get; set; }

    public int? Assigned_To { get; set; }

    public bool BatchPriority { get; set; }
    public bool Tasks_Priority { get; set; }
    public bool Tasks_Push { get; set; }

    public int StepOrder { get; set; }

    public string RowType { get; set; } = ""; // Parent / ParentChildMerged / SingleChild
} 

public class UnassignedTaskDto
{
    public string? WorkCenter { get; set; }
    public int RootId { get; set; }
    public int? WorkCenterSort { get; set; }
    public int TaskId { get; set; }
    public int BatchProductId { get; set; }
    public int ProductToPartId { get; set; }

    public string? BatchCode { get; set; }
    public string? ProductName { get; set; }
    public string? TopPartName { get; set; }
    public string? StepName { get; set; }
    public int StepType { get; set; }
    public int TopPartStepId { get; set; }

    public int Qty { get; set; }
    public string? QtyBreakdown { get; set; }
    public int EstimatedMinutes { get; set; }

    public int Status { get; set; }
    public bool CanStart { get; set; }

    public int? Assigned_To { get; set; }

    public bool BatchPriority { get; set; }
    public bool Tasks_Priority { get; set; }
    public bool Tasks_Push { get; set; }

    public int StepOrder { get; set; }

    public string RowType { get; set; } = "";
}

}