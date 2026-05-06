using Microsoft.AspNetCore.Mvc;
using ManiApi.Services.Orders;
using ManiApi.Models;
using ManiApi.DTOs;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] OrderCreateDto dto)
{
    var orderId = await _orderService.CreateOrder(dto.Order);
    await _orderService.CreateOrderItems(orderId, dto.Items);

    return Ok(orderId);
}

[HttpPost("map")]
public async Task<IActionResult> MapCodes([FromBody] List<string> codes, [FromQuery] string customer)
{
    var result = await _orderService.MapCustomerCodes(customer, codes);
    return Ok(result);
}

}