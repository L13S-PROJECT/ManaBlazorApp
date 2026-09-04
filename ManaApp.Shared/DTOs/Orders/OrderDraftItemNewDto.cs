namespace ManaApp.Shared.DTOs.Orders;

public class OrderDraftItemNewDto
{
    public int Id { get; set; }

    public string CustomerCode { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }

    public int? TopPartId { get; set; }
    public int? WorkflowId { get; set; }
    public int? RalColorId { get; set; }

    public bool IsMapped { get; set; }

    public string? TopPartName { get; set; }
    public string? TopPartCode { get; set; }
    public byte? TopPartType { get; set; }

    public int? WorkflowVersion { get; set; }
    public string? RalColorName { get; set; }
}