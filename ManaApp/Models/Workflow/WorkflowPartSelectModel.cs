namespace ManaApp.Models;

public class WorkflowPartSelectModel
{
    public int TopPartId { get; set; }

    public string TopPartName { get; set; } = "";

    public int Stage { get; set; }
    public int QtyPerProduct { get; set; } = 1;
    public bool Checked { get; set; }

}