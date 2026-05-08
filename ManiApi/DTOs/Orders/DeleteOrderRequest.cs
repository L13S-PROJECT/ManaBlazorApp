namespace ManiApi.DTOs.Orders;

public class DeleteOrderRequest
{
    public int Id { get; set; }

    public string? Comment { get; set; }
}