namespace ManaApp.DTOs.Orders;

public class OrderDraftDto
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public DateTime? OrderDate { get; set; }
}
