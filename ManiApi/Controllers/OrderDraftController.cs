using Microsoft.AspNetCore.Mvc;
using ManiApi.DTOs.Orders;
using ManiApi.Services.Orders;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderDraftController : ControllerBase
{
    private readonly OrderDraftService _draftService;

    public OrderDraftController(OrderDraftService draftService)
    {
        _draftService = draftService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderDraftDto dto)
    {
        var id = await _draftService.CreateDraft(dto);

        return Ok(id);
    }

    [HttpGet("latest")]
public async Task<IActionResult> GetLatest()
{
    var draft = await _draftService.GetLatestDraft();

    if (draft is null)
        return NotFound();

    var items = await _draftService.GetDraftItemDtos(draft.Id);

    return Ok(new
    {
        Draft = draft,
        Items = items
    });
}

[HttpPost("save-map")]
public async Task<IActionResult> SaveMap(
    [FromBody] SaveCustomerCodeMapRequest dto)
{
    await _draftService.SaveCustomerMap(dto);

    return Ok();
}

}