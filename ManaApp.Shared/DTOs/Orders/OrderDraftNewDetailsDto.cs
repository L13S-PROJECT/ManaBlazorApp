namespace ManaApp.Shared.DTOs.Orders;

public class OrderDraftNewDetailsDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime? OrderDate { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Comment { get; set; }

    public List<OrderDraftItemNewDto> Items { get; set; } = [];
}