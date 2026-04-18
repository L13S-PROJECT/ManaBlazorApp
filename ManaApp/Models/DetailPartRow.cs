namespace ManaApp.Models;

public sealed class DetailPartRow
{
    public string TopPartName { get; set; } = "";
    public int Quantity { get; set; }
    public int ProductToPartId { get; set; }

    public int TopPartId { get; set; }  
    public string? WorkCenterName { get; set; }
    public int Qty { get; set; }
    public string QtyDisplay { get; set; } = "";
    public bool IsDone { get; set; } = false;
    public int? AssignedEmployeeId { get; set; }
    public int TopPartIdRaw { get; set; }   // tikai priekš sasaistes ar Taskiem, nav no DB 
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

