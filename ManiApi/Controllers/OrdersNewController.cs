using ManaApp.Shared.DTOs.Orders;
using ManiApi.Services.OrdersNew;
using Microsoft.AspNetCore.Mvc;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersNewController : ControllerBase
{
    private readonly OrderNewService _service;

    public OrdersNewController(OrderNewService service)
    {
        _service = service;
    }

    [HttpPost("from-draft")]
    public async Task<ActionResult<int>> CreateFromDraft(ConfirmOrderDraftNewDto dto)
    {
        var orderId = await _service.CreateFromDraftAsync(dto);

        if (!orderId.HasValue)
            return BadRequest();

        return Ok(orderId.Value);
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderNewListItemDto>>> GetOrders()
    {
        var orders = await _service.GetOrdersAsync();
        return Ok(orders);
    }

    [HttpGet("{orderId:int}")]
    public async Task<ActionResult<OrderNewDetailsDto>> GetOrder(int orderId)
    {
        var order = await _service.GetOrderAsync(orderId);

        if (order == null)
            return NotFound();

        return Ok(order);
    }

    [HttpDelete("{orderId:int}")]
    public async Task<IActionResult> Delete(int orderId)
    {
        var deleted = await _service.DeleteAsync(orderId);

        if (!deleted)
            return NotFound();

        return Ok();
    }

    [HttpPut("{orderId:int}/comment")]
    public async Task<IActionResult> UpdateComment(
        int orderId,
        UpdateOrderCommentNewDto dto)
    {
        var updated = await _service.UpdateCommentAsync(orderId, dto.Comment);

        if (!updated)
            return NotFound();

        return Ok();
    }

}