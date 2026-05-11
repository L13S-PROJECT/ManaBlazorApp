using Microsoft.AspNetCore.Mvc;
using ManiApi.Services.Orders;
using ManiApi.Models;
using ManiApi.DTOs;
using ManiApi.DTOs.Orders;

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

[HttpPost("save-draft")]
public async Task<IActionResult> SaveDraft(
    [FromBody] SaveOrderRequest request)
{
    try
    {
        await _orderService.SaveDraftOrder(
            request.OrderNumber,
            request.Comment);

        return Ok();
    }
    catch (Exception ex)
    {
        return BadRequest(ex.ToString());
    }
}

[HttpGet]
public async Task<IActionResult> GetOrders(
    [FromQuery] GetOrdersRequest request)
{
    var result = await _orderService.GetOrders(request);
    return Ok(result);
}

[HttpPost("delete")]
public async Task<IActionResult> DeleteOrder(
    [FromBody] DeleteOrderRequest request)
{
    await _orderService.DeleteOrder(request);

    return Ok();
}

[HttpPost("comment")]
public async Task<IActionResult> UpdateComment(
    [FromBody] UpdateOrderCommentRequest request)
{
    await _orderService.UpdateComment(request);

    return Ok();
}

[HttpGet("{orderId}/items")]
public async Task<IActionResult> GetOrderItems(int orderId)
{
    var result = await _orderService.GetOrderItems(orderId);

    return Ok(result);
}

}