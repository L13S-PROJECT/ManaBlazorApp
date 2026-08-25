namespace ManaApp.Shared.DTOs.TopPart;

public class TopPartWorkflowBomDto
{
    public int Id { get; set; }

    public byte ComponentType { get; set; }

    public uint? TopPartId { get; set; }

    public int? ItemId { get; set; }

    public int? ReferencedWorkflowId { get; set; }

    public int? RequiredWorkflowNodeId { get; set; }

    public string? RequiredWorkflowNodeName { get; set; }

    public decimal Quantity { get; set; }
    public decimal UsedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    public string? TopPartCode { get; set; }

    public string? TopPartName { get; set; }

    public string? ItemCode { get; set; }

    public string? ItemName { get; set; }

    public string? ItemUnit { get; set; }

}