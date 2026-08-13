namespace ManaApp.Shared.DTOs.TopPart;

public class AddTopPartProcessComponentRequest
{
    public int ProcessNodeId { get; set; }
    public int WorkflowComponentId { get; set; }
    public decimal Quantity { get; set; }
}