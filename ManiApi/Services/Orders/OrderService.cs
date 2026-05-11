using ManiApi.Data;
using ManiApi.Models;
using ManiApi.DTOs.Orders;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.Orders;

public class OrderService
{
    private readonly AppDbContext _db;

public OrderService(AppDbContext db)
    {
        _db = db;
    }

public async Task<int> CreateOrder(Order order)
    {
        var exists = _db.Orders.Any(o =>
            o.OrderNumber == order.OrderNumber &&
            o.CustomerName == order.CustomerName &&
            o.IsActive);

        if (exists)
        {
            throw new Exception("Tāds pasūtījuma numurs jau pievienots.");
        }
        
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return order.Id;
    }

public async Task CreateOrderItems(int orderId, List<OrderItem> items)
    {
        foreach (var item in items)
        {
            item.OrderId = orderId;
            _db.OrderItems.Add(item);
        }

        await _db.SaveChangesAsync();
    }

public async Task<List<object>> MapCustomerCodes(string customer, List<string> codes)
{
    var maps = _db.CustomerCodeMaps
        .Where(m => m.CustomerName == customer && codes.Contains(m.CustomerCode))
        .ToList();

    var result = codes.Select(code =>
    {
        var match = maps.FirstOrDefault(m => m.CustomerCode == code);

        return new
    {
        CustomerCode = code,
        VersionId = match?.VersionId,
        ProductToPartId = match?.ProductToPartId,
        RalColorId = match?.RalColorId,
        IsProduct = match?.IsProduct,
        IsPart = match?.IsPart,
        IsMapped = match != null
    };

    }).ToList<object>();

    return await Task.FromResult(result);
}

public async Task SaveDraftOrder(
    string orderNumber,
    string? comment)

{
    var draft = await _db.OrderDrafts
        .FirstOrDefaultAsync(x =>
            x.OrderNumber == orderNumber);

    if (draft is null)
    {
        throw new Exception("Pasūtījuma melnraksts nav atrasts.");
    }

    var exists = await _db.Orders.AnyAsync(x =>
    x.OrderNumber == draft.OrderNumber &&
    x.CustomerName == draft.CustomerName &&
    x.IsActive);

if (exists)
{
    throw new Exception(
        "Tāds pasūtījums jau eksistē.");
}

var draftItems = await _db.OrderDraftItems
    .Where(x => x.OrderDraftId == draft.Id)
    .ToListAsync();

var order = new Order
{
    OrderNumber = draft.OrderNumber,
    CustomerName = draft.CustomerName,
    Comment = comment,
    OrderDate = draft.OrderDate,
    CreatedAt = DateTime.UtcNow,
    IsActive = true
};

_db.Orders.Add(order);

await _db.SaveChangesAsync();

foreach (var item in draftItems)
{
    _db.OrderItems.Add(new OrderItem
    {
        OrderId = order.Id,

        CustomerCode = item.CustomerCode,
        Name = item.Name,
        Quantity = item.Quantity,

        CustomerCodeMapId = item.CustomerCodeMapId,

        IsActive = true
    });
}

await _db.SaveChangesAsync();

_db.OrderDraftItems.RemoveRange(draftItems);

_db.OrderDrafts.Remove(draft);

await _db.SaveChangesAsync();


}

public async Task<List<OrderListDto>> GetOrders(
    GetOrdersRequest request)
{
    var query = _db.Orders
    .AsQueryable();

        if (request.ShowArchived)
        {
            query = query.Where(x => !x.IsActive);
        }
        else
        {
            query = query.Where(x => x.IsActive);
        }

if (!string.IsNullOrWhiteSpace(request.Search))
{
    query = query.Where(x =>
        x.OrderNumber.Contains(request.Search) ||
        x.CustomerName.Contains(request.Search) ||
        (x.Comment != null &&
         x.Comment.Contains(request.Search)));
}

if (request.DateFrom.HasValue)
{
    query = query.Where(x =>
        x.OrderDate >= request.DateFrom.Value);
}

if (request.DateTo.HasValue)
{
    query = query.Where(x =>
        x.OrderDate <= request.DateTo.Value);
}
    
    return await query
        .Select(x => new OrderListDto
        {
            Id = x.Id,
            OrderNumber = x.OrderNumber,
            OrderDate = x.OrderDate,
            CustomerName = x.CustomerName,
            Comment = x.Comment
        })
        .ToListAsync();
}

public async Task DeleteOrder(
    DeleteOrderRequest request)
{
    var order = await _db.Orders
        .FirstOrDefaultAsync(x =>
            x.Id == request.Id);

    if (order is null)
    {
        throw new Exception(
            "Pasūtījums nav atrasts.");
    }

    order.IsActive = false;

    var deleteText =
    $"DZĒSTS: {request.Comment}";

        if (string.IsNullOrWhiteSpace(order.Comment))
        {
            order.Comment = deleteText;
        }
        else
        {
            order.Comment +=
                $"\n---\n{deleteText}";
        }
}

public async Task UpdateComment(
    UpdateOrderCommentRequest request)
{
var order = await _db.Orders
    .FirstOrDefaultAsync(x =>
        x.Id == request.Id);

if (order is null)
{
    throw new Exception(
        "Pasūtījums nav atrasts.");
}

order.Comment = request.Comment;

await _db.SaveChangesAsync();
}

public async Task<List<object>> GetOrderItems(
    int orderId)
{
    var result = await (
        from oi in _db.OrderItems

        join map in _db.CustomerCodeMaps
            on oi.CustomerCodeMapId equals map.Id into mapJoin

        from map in mapJoin.DefaultIfEmpty()

        join version in _db.ProductVersions
            on map.VersionId equals version.Id into versionJoin

        from version in versionJoin.DefaultIfEmpty()

        join product in _db.Products
            on version.ProductId equals product.Id into productJoin

        from product in productJoin.DefaultIfEmpty()
                join topPart in _db.TopParts
            on map.TopPartId equals topPart.Id into topPartJoin

        from topPart in topPartJoin.DefaultIfEmpty()

        join ral in _db.RalColors
            on map.RalColorId equals ral.ID into ralJoin

        from ral in ralJoin.DefaultIfEmpty()

        where
            oi.OrderId == orderId &&
            oi.IsActive

        orderby oi.Id

        select new
        {
            oi.Id,
            CustomerCode = oi.CustomerCode,

            ProductCode =
                product != null
                    ? product.ProductCode
                    : "-",
            Name =
            product != null
                ? product.ProductName
                : oi.Name,
            oi.Quantity,

            VersionName =
                version != null
                    ? version.VersionName
                    : null,
            ItemType =
                map != null && map.IsProduct
                    ? "Prece"
                    : map != null && map.IsPart
                        ? "Detaļa"
                        : "-",
            TopPartName =
                topPart != null
                    ? topPart.TopPartName
                    : "-",
            RalName =
                ral != null
                    ? ral.Name
                    : "-",

        }

    ).ToListAsync();

    return result.Cast<object>().ToList();
}

}