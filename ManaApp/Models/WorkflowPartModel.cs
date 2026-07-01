namespace ManaApp.Models;

public class WorkflowPartModel
{
    public int TopPartId { get; set; }

    public string TopPartCode { get; set; } = "";

    public string TopPartName { get; set; } = "";
    public int QtyPerProduct { get; set; } = 1;
    public int Stage { get; set; }
}