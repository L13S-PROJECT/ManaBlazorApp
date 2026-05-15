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
    var orderNumber = dto.Header.OrderNumber.Trim();

    var existsInDrafts = await _db.OrderDrafts
        .AnyAsync(x => x.OrderNumber.Trim() == orderNumber);

    if (existsInDrafts)
    {
        throw new Exception(
            $"Pasūtījums ar numuru '{orderNumber}' jau eksistē melnrakstos.");
    }

    var existsInOrders = await _db.Orders
        .AnyAsync(x => x.OrderNumber.Trim() == orderNumber);

    if (existsInOrders)
    {
        throw new Exception(
            $"Pasūtījums ar numuru '{orderNumber}' jau ir saglabāts.");
    }

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
                VersionId = map?.VersionId,
                ProductToPartId = map?.ProductToPartId,
                RalColorId = map?.RalColorId,
                TopPartId = map?.TopPartId,

                IsProduct = map?.IsProduct ?? false,
                IsPart = map?.IsPart ?? false,

                IsMapped = false,

                CustomerCodeMapId = map?.Id,

                IsActive = true
            });
    }

    await _db.SaveChangesAsync();

    return draft.Id;
}

public async Task<List<OrderDraft>> GetDrafts()
{
    return await _db.OrderDrafts
        .OrderByDescending(x => x.Id)
        .ToListAsync();
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
    var draftItem = await _db.OrderDraftItems
    .FirstOrDefaultAsync(x => x.Id == dto.OrderDraftItemId);

        if (draftItem is null)
            return;
    
    bool duplicateExists;

            if (dto.IsProduct)
            {
                duplicateExists = await _db.CustomerCodeMaps
                    .AnyAsync(x =>
                        x.Id != draftItem.CustomerCodeMapId &&
                        x.VersionId == dto.VersionId &&
                        x.RalColorId == dto.RalColorId &&
                        x.IsProduct);
            }
            else
            {
                duplicateExists = await _db.CustomerCodeMaps
                    .AnyAsync(x =>
                        x.Id != draftItem.CustomerCodeMapId &&
                        x.VersionId == dto.VersionId &&
                        x.ProductToPartId == dto.ProductToPartId &&
                        x.RalColorId == dto.RalColorId &&
                        x.IsPart);
            }

            if (duplicateExists)
            {
                throw new Exception(
                    "Šādai konfigurācijai klienta kods jau eksistē.");
            }

    var existing = await _db.CustomerCodeMaps
        .FirstOrDefaultAsync(x =>
            x.CustomerName == dto.CustomerName &&
            x.CustomerCode == dto.CustomerCode);

    if (existing is null)
            {
                existing = new CustomerCodeMap();

                _db.CustomerCodeMaps.Add(existing);

                await _db.SaveChangesAsync();
            }

    existing.CustomerName = dto.CustomerName;
    existing.CustomerCode = dto.CustomerCode;

    existing.VersionId = dto.VersionId;
    existing.ProductToPartId = dto.ProductToPartId;
    existing.TopPartId = dto.TopPartId;
    existing.RalColorId = dto.RalColorId;

    existing.IsProduct = dto.IsProduct;
    existing.IsPart = dto.IsPart;

    draftItem.VersionId = dto.VersionId;
    draftItem.ProductToPartId = dto.ProductToPartId;
    draftItem.TopPartId = dto.TopPartId;
    draftItem.RalColorId = dto.RalColorId;

    draftItem.IsProduct = dto.IsProduct;
    draftItem.IsPart = dto.IsPart;

    draftItem.CustomerCodeMapId = existing.Id;

    draftItem.IsMapped = true;

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

    var result = items.Select(x =>
    {
        var version = versions
            .FirstOrDefault(v => v.Id == x.VersionId);

        var product = version is null
            ? null
            : products.FirstOrDefault(p => p.Id == version.ProductId);

        return new OrderDraftItemDto
        {
            Id = x.Id,
            CustomerCode = x.CustomerCode,
            Name = x.Name,
            Quantity = x.Quantity,

            VersionId = x.VersionId,
            ProductToPartId = x.ProductToPartId,
            RalColorId = x.RalColorId,
            TopPartId = x.TopPartId,

            CustomerCodeMapId = x.CustomerCodeMapId,

            IsMapped = x.IsMapped,
            IsProduct = x.IsProduct,
            IsPart = x.IsPart,

            ProductName = product?.ProductName,

            VersionName = version?.VersionName,
            VersionIsActive = version?.IsActive ?? false,

            RalColorName = ralColors
                .FirstOrDefault(r => r.ID == x.RalColorId)
                ?.Name,

            TopPartName = topParts
                .FirstOrDefault(t => t.Id == x.TopPartId)
                ?.TopPartName,

            MappingType =
                x.IsProduct == true ? "Product" :
                x.IsPart == true ? "Part" :
                null
        };
    }).ToList();

    return result;
}

public async Task DeleteDraft(int draftId)
{
    var items = await _db.OrderDraftItems
        .Where(x => x.OrderDraftId == draftId)
        .ToListAsync();

    _db.OrderDraftItems.RemoveRange(items);

    var draft = await _db.OrderDrafts
        .FirstOrDefaultAsync(x => x.Id == draftId);

    if (draft is not null)
    {
        _db.OrderDrafts.Remove(draft);
    }

    await _db.SaveChangesAsync();
}

public async Task<OrderDraft?> GetDraftById(int draftId)
{
    return await _db.OrderDrafts
        .FirstOrDefaultAsync(x => x.Id == draftId);
}

}