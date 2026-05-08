namespace ManiApi.DTOs.Orders;

public class SaveOrderRequest
{
    public string OrderNumber { get; set; } = "";
    public string? Comment { get; set; }
}
