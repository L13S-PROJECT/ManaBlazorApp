using ManaApp.Shared.DTOs.Orders;
using ManiApi.Services.OrdersNew;
using Microsoft.AspNetCore.Mvc;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderDraftNewController : ControllerBase
{
    private readonly OrderDraftNewService _service;

    public OrderDraftNewController(OrderDraftNewService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create(CreateOrderDraftNewDto dto)
    {
        var draftId = await _service.CreateAsync(dto);
        return Ok(draftId);
    }

    [HttpGet("{draftId:int}")]
    public async Task<ActionResult<OrderDraftNewDetailsDto>> Get(int draftId)
    {
        var draft = await _service.GetAsync(draftId);

        if (draft == null)
            return NotFound();

        return Ok(draft);
    }

    [HttpPost("save-map")]
    public async Task<IActionResult> SaveMap(SaveCustomerCodeMapNewDto dto)
    {
        var saved = await _service.SaveMapAsync(dto);

        if (!saved)
            return NotFound();

        return Ok();
    }

    [HttpGet("latest")]
    public async Task<ActionResult<OrderDraftNewDetailsDto>> GetLatest()
    {
        var draft = await _service.GetLatestAsync();

        if (draft == null)
            return NotFound();

        return Ok(draft);
    }

    [HttpDelete("{draftId:int}")]
    public async Task<IActionResult> Delete(int draftId)
    {
        var deleted = await _service.DeleteAsync(draftId);

        if (!deleted)
            return NotFound();

        return Ok();
    }

}