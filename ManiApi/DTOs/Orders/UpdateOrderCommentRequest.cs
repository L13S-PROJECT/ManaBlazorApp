namespace ManiApi.DTOs.Orders;

public class UpdateOrderCommentRequest
{
    public int Id { get; set; }

    public string? Comment { get; set; }
}