using ManiApi.Data;
using ManaApp.Shared.DTOs.Orders;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.OrdersNew;

public class OrderNewService
{
    private readonly AppDbContext _db;

    public OrderNewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int?> CreateFromDraftAsync(ConfirmOrderDraftNewDto dto)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var draft = await _db.OrderDraftsNew
                .FirstOrDefaultAsync(x => x.Id == dto.DraftId && x.IsActive);

            if (draft == null)
                return null;

            var items = await _db.OrderDraftItemsNew
                .Where(x => x.OrderDraftId == dto.DraftId && x.IsActive)
                .ToListAsync();

            if (items.Count == 0 || items.Any(x => !x.IsMapped || !x.TopPartId.HasValue || !x.WorkflowId.HasValue))
                return null;

            var order = new OrderNew
            {
                OrderNumber = draft.OrderNumber,
                OrderDate = draft.OrderDate,
                CustomerName = draft.CustomerName,
                CreatedAt = DateTime.UtcNow,
                Comment = dto.Comment,
                IsActive = true
            };

            _db.OrdersNew.Add(order);
            await _db.SaveChangesAsync();

            foreach (var item in items)
            {
                _db.OrderItemsNew.Add(new OrderItemNew
                {
                    OrderId = order.Id,
                    CustomerCode = item.CustomerCode,
                    Name = item.Name,
                    Quantity = item.Quantity,
                    TopPartId = item.TopPartId!.Value,
                    WorkflowId = item.WorkflowId!.Value,
                    RalColorId = item.RalColorId,
                    IsActive = true
                });
            }

            draft.IsActive = false;

            foreach (var item in items)
            {
                item.IsActive = false;
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return order.Id;
        }

    public async Task<List<OrderNewListItemDto>> GetOrdersAsync()
        {
            return await _db.OrdersNew
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new OrderNewListItemDto
                {
                    Id = x.Id,
                    OrderNumber = x.OrderNumber,
                    OrderDate = x.OrderDate,
                    CustomerName = x.CustomerName,
                    Comment = x.Comment,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

    public async Task<OrderNewDetailsDto?> GetOrderAsync(int orderId)
        {
            var order = await _db.OrdersNew
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == orderId && x.IsActive);

            if (order == null)
                return null;

            var items = await _db.OrderItemsNew
                .AsNoTracking()
                .Where(x => x.OrderId == orderId && x.IsActive)
                .Select(x => new OrderNewItemDto
                {
                    Id = x.Id,
                    CustomerCode = x.CustomerCode,
                    Name = x.Name,
                    Quantity = x.Quantity,
                    TopPartId = x.TopPartId,
                    WorkflowId = x.WorkflowId,
                    RalColorId = x.RalColorId
                })
                .ToListAsync();
            
            var topPartIds = items
                .Select(x => x.TopPartId)
                .Distinct()
                .ToList();

            var topParts = await _db.TopParts
                .AsNoTracking()
                .Where(x => topPartIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var workflowIds = items
                .Select(x => x.WorkflowId)
                .Distinct()
                .ToList();

            var workflows = await _db.Workflows
                .AsNoTracking()
                .Where(x => workflowIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var ralColorIds = items
                .Where(x => x.RalColorId.HasValue)
                .Select(x => x.RalColorId!.Value)
                .Distinct()
                .ToList();

            var ralColors = await _db.RalColors
                .AsNoTracking()
                .Where(x => ralColorIds.Contains(x.ID))
                .ToDictionaryAsync(x => x.ID);

            foreach (var item in items)
            {
                if (topParts.TryGetValue(item.TopPartId, out var topPart))
                {
                    item.TopPartName = topPart.TopPartName;
                    item.TopPartCode = topPart.TopPartCode;
                    item.TopPartType = (byte)topPart.TopPartType;
                }

                if (workflows.TryGetValue(item.WorkflowId, out var workflow))
                {
                    item.WorkflowVersion = workflow.WorkflowVersion;
                }

                if (item.RalColorId.HasValue &&
                    ralColors.TryGetValue(item.RalColorId.Value, out var ralColor))
                {
                    item.RalColorName = ralColor.Name;
                }

            }

            return new OrderNewDetailsDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                CustomerName = order.CustomerName,
                Comment = order.Comment,
                Items = items
            };

            
        }

        public async Task<bool> DeleteAsync(int orderId)
        {
            var order = await _db.OrdersNew
                .FirstOrDefaultAsync(x => x.Id == orderId && x.IsActive);

            if (order == null)
                return false;

            order.IsActive = false;

            var items = await _db.OrderItemsNew
                .Where(x => x.OrderId == orderId && x.IsActive)
                .ToListAsync();

            foreach (var item in items)
                item.IsActive = false;

            await _db.SaveChangesAsync();

            return true;
        }

    public async Task<bool> UpdateCommentAsync(int orderId, string? comment)
        {
            var order = await _db.OrdersNew
                .FirstOrDefaultAsync(x => x.Id == orderId && x.IsActive);

            if (order == null)
                return false;

            order.Comment = comment;

            await _db.SaveChangesAsync();

            return true;
        }

}