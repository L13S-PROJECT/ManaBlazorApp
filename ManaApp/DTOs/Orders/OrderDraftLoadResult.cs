using ManaApp.Models.Orders;

namespace ManaApp.DTOs.Orders;

public class OrderDraftLoadResult
{
    public OrderDraftDto Draft { get; set; } = new();

    public List<OrderDraftItemDto> Items { get; set; } = new();
}