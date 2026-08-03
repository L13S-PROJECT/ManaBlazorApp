namespace ManaApp.Shared.DTOs.Workflow;

public class WorkflowPartModel
{
    public int TopPartId { get; set; }

    public string TopPartCode { get; set; } = "";
    public int ProductToPartId { get; set; }
    public int WorkflowNodeId { get; set; }
    public string TopPartName { get; set; } = "";
    public int QtyPerProduct { get; set; } = 1;
    public int Stage { get; set; }

    public int? ParentProductTopPartId { get; set; }
    public int? AttachToNodeId { get; set; }
    
}