namespace ManaApp.Shared.DTOs.TopPart;

public class AddTopPartBomPartRequest
{
    public int TopPartId { get; set; }
    public int WorkflowId { get; set; }
    public int RequiredWorkflowNodeId { get; set; }
    public decimal Quantity { get; set; }
}