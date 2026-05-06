using ManiApi.Data;
using ManiApi.Models;
using ManiApi.DTOs.Orders;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Orders;

public class OrderDraftService
{
    private readonly AppDbContext _db;

    public OrderDraftService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateDraft(CreateOrderDraftDto dto)
{
    var draft = new OrderDraft
    {
        CreatedAt = DateTime.UtcNow,
        OrderNumber = dto.Header.OrderNumber,
        CustomerName = dto.Header.Customer,
        IsCompleted = false
    };

    if (DateTime.TryParse(dto.Header.Date, out var parsedDate))
    {
        draft.OrderDate = parsedDate;
    }

    _db.OrderDrafts.Add(draft);

    await _db.SaveChangesAsync();

    var customerMaps = await _db.CustomerCodeMaps
    .Where(x => x.CustomerName == dto.Header.Customer)
    .ToListAsync();

    foreach (var item in dto.Items)
    {
        var map = customerMaps.FirstOrDefault(x =>
            x.CustomerCode == item.Code);

        _db.OrderDraftItems.Add(new OrderDraftItem
            {
                OrderDraftId = draft.Id,
                CustomerCode = item.Code,
                Name = item.Name,
                Quantity = item.Quantity,

                VersionId = map?.VersionId,
                ProductToPartId = map?.ProductToPartId,
                RalColorId = map?.RalColorId,

                IsMapped = map is not null,

                IsActive = true
            });
    }

    await _db.SaveChangesAsync();

    return draft.Id;
}

public async Task<OrderDraft?> GetLatestDraft()
{
    return await _db.OrderDrafts
        .OrderByDescending(x => x.Id)
        .FirstOrDefaultAsync();
}

public async Task<List<OrderDraftItem>> GetDraftItems(int draftId)
{
    return await _db.OrderDraftItems
        .Where(x => x.OrderDraftId == draftId)
        .ToListAsync();
}

}