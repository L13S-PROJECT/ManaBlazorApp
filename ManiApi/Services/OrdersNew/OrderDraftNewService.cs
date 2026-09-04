using ManiApi.Data;
using ManiApi.Models;
using ManaApp.Shared.DTOs.Orders;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.OrdersNew;

public class OrderDraftNewService
{
    private readonly AppDbContext _db;

    public OrderDraftNewService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateAsync(CreateOrderDraftNewDto dto)
        {
            DateTime? orderDate = null;

            if (!string.IsNullOrWhiteSpace(dto.Header.Date) &&
                DateTime.TryParse(dto.Header.Date, out var parsedDate))
            {
                orderDate = parsedDate;
            }

            var draft = new OrderDraftNew
            {
                OrderNumber = dto.Header.OrderNumber,
                OrderDate = orderDate,
                CustomerName = dto.Header.Customer,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.OrderDraftsNew.Add(draft);
            await _db.SaveChangesAsync();

            var customerMaps = await _db.CustomerCodeMapsNew
                .AsNoTracking()
                .Where(x =>
                    x.CustomerName == dto.Header.Customer &&
                    x.IsActive)
                .ToDictionaryAsync(x => x.CustomerCode);

            foreach (var item in dto.Items)
            {
                customerMaps.TryGetValue(item.Code, out var map);

                    if (map != null)
                        {
                            var validTopPart = await _db.TopParts
                                .AsNoTracking()
                                .AnyAsync(x =>
                                    x.Id == map.TopPartId &&
                                    x.IsActive &&
                                    (x.TopPartType == TopPartType.Product ||
                                    x.TopPartType == TopPartType.SparePart));

                            var validWorkflow = await _db.Workflows
                                .AsNoTracking()
                                .AnyAsync(x =>
                                    x.Id == map.WorkflowId &&
                                    x.TopPartId == (uint)map.TopPartId &&
                                    x.IsActive &&
                                    x.Status == WorkflowStatus.Released);

                            if (!validTopPart || !validWorkflow)
                                map = null;
                        }
                
                _db.OrderDraftItemsNew.Add(new OrderDraftItemNew
                    {
                        OrderDraftId = draft.Id,
                        CustomerCode = item.Code,
                        Name = item.Name,
                        Quantity = item.Quantity,

                        TopPartId = map?.TopPartId,
                        WorkflowId = map?.WorkflowId,
                        RalColorId = map?.RalColorId,
                        IsMapped = map != null,

                        IsActive = true
                    });
            }

            await _db.SaveChangesAsync();

            return draft.Id;
        }

    public async Task<OrderDraftNewDetailsDto?> GetAsync(int draftId)
        {
            var draft = await _db.OrderDraftsNew
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == draftId && x.IsActive);

            if (draft == null)
                return null;

            var items = await _db.OrderDraftItemsNew
                .AsNoTracking()
                .Where(x => x.OrderDraftId == draftId && x.IsActive)
                .Select(x => new OrderDraftItemNewDto
                {
                    Id = x.Id,
                    CustomerCode = x.CustomerCode,
                    Name = x.Name,
                    Quantity = x.Quantity,
                    TopPartId = x.TopPartId,
                    WorkflowId = x.WorkflowId,
                    RalColorId = x.RalColorId,
                    IsMapped = x.IsMapped
                })
                .ToListAsync();
            
            var topPartIds = items
                .Where(x => x.TopPartId.HasValue)
                .Select(x => x.TopPartId!.Value)
                .Distinct()
                .ToList();

            var topParts = await _db.TopParts
                .AsNoTracking()
                .Where(x => topPartIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            var workflowIds = items
                .Where(x => x.WorkflowId.HasValue)
                .Select(x => x.WorkflowId!.Value)
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
                if (item.TopPartId.HasValue &&
                    topParts.TryGetValue(item.TopPartId.Value, out var topPart))
                {
                    item.TopPartName = topPart.TopPartName;
                    item.TopPartCode = topPart.TopPartCode;
                    item.TopPartType = (byte)topPart.TopPartType;
                }

                if (item.WorkflowId.HasValue &&
                    workflows.TryGetValue(item.WorkflowId.Value, out var workflow))
                {
                    item.WorkflowVersion = workflow.WorkflowVersion;
                }

                if (item.RalColorId.HasValue &&
                    ralColors.TryGetValue(item.RalColorId.Value, out var ralColor))
                {
                    item.RalColorName = ralColor.Name;
                }

            }
            
            return new OrderDraftNewDetailsDto
            {
                Id = draft.Id,
                OrderNumber = draft.OrderNumber,
                OrderDate = draft.OrderDate,
                CustomerName = draft.CustomerName,
                Comment = draft.Comment,
                Items = items
            };
        }

        public async Task<bool> SaveMapAsync(SaveCustomerCodeMapNewDto dto)
            {
                var item = await _db.OrderDraftItemsNew
                    .FirstOrDefaultAsync(x => x.Id == dto.OrderDraftItemId && x.IsActive);

                if (item == null)
                    return false;
                
                var topPart = await _db.TopParts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.TopPartId &&
                        x.IsActive &&
                        (x.TopPartType == TopPartType.Product ||
                        x.TopPartType == TopPartType.SparePart));

                if (topPart == null)
                    return false;

                var workflow = await _db.Workflows
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowId &&
                        x.IsActive &&
                        x.Status == WorkflowStatus.Released &&
                        x.TopPartId == (uint)dto.TopPartId);

                if (workflow == null)
                    return false;

                item.TopPartId = dto.TopPartId;
                item.WorkflowId = dto.WorkflowId;
                item.RalColorId = dto.RalColorId;
                item.IsMapped = true;

                var existingMap = await _db.CustomerCodeMapsNew
                    .FirstOrDefaultAsync(x =>
                        x.CustomerName == dto.CustomerName &&
                        x.CustomerCode == dto.CustomerCode);

                if (existingMap == null)
                {
                    _db.CustomerCodeMapsNew.Add(new CustomerCodeMapNew
                    {
                        CustomerName = dto.CustomerName,
                        CustomerCode = dto.CustomerCode,
                        TopPartId = dto.TopPartId,
                        WorkflowId = dto.WorkflowId,
                        RalColorId = dto.RalColorId,
                        IsActive = true
                    });
                }
                else
                {
                    existingMap.TopPartId = dto.TopPartId;
                    existingMap.WorkflowId = dto.WorkflowId;
                    existingMap.RalColorId = dto.RalColorId;
                    existingMap.IsActive = true;
                }

                await _db.SaveChangesAsync();

                return true;
            }

        public async Task<OrderDraftNewDetailsDto?> GetLatestAsync()
            {
                var draftId = await _db.OrderDraftsNew
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync();

                if (draftId == 0)
                    return null;

                return await GetAsync(draftId);
            }

        public async Task<bool> DeleteAsync(int draftId)
            {
                var draft = await _db.OrderDraftsNew
                    .FirstOrDefaultAsync(x => x.Id == draftId && x.IsActive);

                if (draft == null)
                    return false;

                draft.IsActive = false;

                var items = await _db.OrderDraftItemsNew
                    .Where(x => x.OrderDraftId == draftId && x.IsActive)
                    .ToListAsync();

                foreach (var item in items)
                    item.IsActive = false;

                await _db.SaveChangesAsync();

                return true;
            }

}