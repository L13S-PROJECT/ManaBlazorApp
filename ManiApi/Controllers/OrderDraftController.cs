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
        try
        {
            var id = await _draftService.CreateDraft(dto);

            return Ok(id);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("latest")]
public async Task<IActionResult> GetLatest()
{
    var drafts = await _draftService.GetDrafts();

    return Ok(drafts);

}

[HttpGet("{draftId}")]
public async Task<IActionResult> GetDraft(int draftId)
{
    var draft = await _draftService
        .GetDraftById(draftId);

    if (draft is null)
        return NotFound();

    var items = await _draftService
        .GetDraftItemDtos(draft.Id);

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
    try
    {
        await _draftService.SaveCustomerMap(dto);

        return Ok();
    }
    catch (Exception ex)
    {
        return BadRequest(ex.Message);
    }
}

[HttpDelete("{draftId}")]
public async Task<IActionResult> DeleteDraft(int draftId)
{
    await _draftService.DeleteDraft(draftId);

    return Ok();
}

}