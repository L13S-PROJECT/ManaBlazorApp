using ManiApi.Data;
using ManiApi.Models;

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



}