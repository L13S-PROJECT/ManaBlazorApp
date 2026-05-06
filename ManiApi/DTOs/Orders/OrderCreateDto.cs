using ManiApi.Models;

namespace ManiApi.DTOs;

public class OrderCreateDto
{
    public Order Order { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
}