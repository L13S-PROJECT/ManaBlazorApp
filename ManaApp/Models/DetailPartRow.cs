namespace ManaApp.Models;

public sealed class DetailPartRow
{
    public string TopPartName { get; set; } = "";
    public int Quantity { get; set; }
    public int ProductToPartId { get; set; }

    public string? WorkCenterName { get; set; }

    public bool IsDone { get; set; } = false;
    public int? AssignedEmployeeId { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public string? AcceptedByEmployeeName { get; set; }
    public string? Comment { get; set; }

    public List<TopPartStepRow> Steps { get; set; } = new();
    
}

public sealed class TopPartStepRow
{
    public int Id { get; set; }
    public int ProductToPartId { get; set; }   // ✅ obligāti sasaistīšanai ar DetailPartRow
    public string StepName { get; set; } = "";
}
