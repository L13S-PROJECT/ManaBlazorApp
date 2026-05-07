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

    var customerName = dto.Header.Customer.Trim();

    var customerMaps = await _db.CustomerCodeMaps
        .Where(x =>
            x.CustomerName.Trim().ToUpper() ==
            customerName.ToUpper())
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

                IsMapped = map is not null,

                CustomerCodeMapId = map?.Id,

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

public async Task SaveCustomerMap(
    SaveCustomerCodeMapRequest dto)
{
    var existing = await _db.CustomerCodeMaps
        .FirstOrDefaultAsync(x =>
            x.CustomerName == dto.CustomerName &&
            x.CustomerCode == dto.CustomerCode);

    if (existing is null)
    {
        existing = new CustomerCodeMap();

        _db.CustomerCodeMaps.Add(existing);
    }

    existing.CustomerName = dto.CustomerName;
    existing.CustomerCode = dto.CustomerCode;

    existing.VersionId = dto.VersionId;
    existing.ProductToPartId = dto.ProductToPartId;
    existing.TopPartId = dto.TopPartId;
    existing.RalColorId = dto.RalColorId;

    existing.IsProduct = dto.IsProduct;
    existing.IsPart = dto.IsPart;

    await _db.SaveChangesAsync();

    var draftItems = await _db.OrderDraftItems
    .Where(x =>
        x.CustomerCode == dto.CustomerCode)
    .ToListAsync();

foreach (var item in draftItems)
{
    item.CustomerCodeMapId = existing.Id;

    item.IsMapped = true;
}

await _db.SaveChangesAsync();
}

public async Task<List<OrderDraftItemDto>> GetDraftItemDtos(int draftId)
{
    var items = await _db.OrderDraftItems
        .Where(x => x.OrderDraftId == draftId)
        .ToListAsync();

    var versions = await _db.Set<ProductVersion>().ToListAsync();

    var ralColors = await _db.Set<RalColor>().ToListAsync();

    var topParts = await _db.Set<TopPart>().ToListAsync();

    var products = await _db.Set<Product>().ToListAsync();

    var customerMaps = await _db.CustomerCodeMaps
    .ToListAsync();

    var result = items.Select(x =>
    {
        var map = customerMaps
    .FirstOrDefault(m => m.Id == x.CustomerCodeMapId);

    Console.WriteLine(
    $"{x.CustomerCode} -> mapId={x.CustomerCodeMapId}");

        var version = versions
            .FirstOrDefault(v => v.Id == map?.VersionId);

        var product = version is null
            ? null
            : products.FirstOrDefault(p => p.Id == version.ProductId);

        return new OrderDraftItemDto
        {
            CustomerCode = x.CustomerCode,
            Name = x.Name,
            Quantity = x.Quantity,

            VersionId = map?.VersionId,
            ProductToPartId = map?.ProductToPartId,
            RalColorId = map?.RalColorId,
            TopPartId = map?.TopPartId,

            IsMapped = map is not null,
            IsProduct = map?.IsProduct ?? false,
            IsPart = map?.IsPart ?? false,

            ProductName = product?.ProductName,

            VersionName = version?.VersionName,

            RalColorName = ralColors
                .FirstOrDefault(r => r.ID == map?.RalColorId)
                ?.Name,

            TopPartName = topParts
                .FirstOrDefault(t => t.Id == map?.TopPartId)
                ?.TopPartName,

            MappingType =
                map?.IsProduct == true ? "Product" :
                map?.IsPart == true ? "Part" :
                null
        };
    }).ToList();

    return result;
}

}