using System.Net.Http.Json;
using ManaApp.Models;
using ManaApp.Shared.DTOs.Planning;
using ManaApp.Shared.DTOs.Batches;

namespace ManaApp.Services.Planning;

public class PlanningDraftService
{
    private readonly HttpClient _http;
    private readonly PlanningLookupService _planningLookupService;

    public PlanningDraftService(
        HttpClient http,
        PlanningLookupService planningLookupService)
    {
        _http = http;
        _planningLookupService = planningLookupService;
    }

    public async Task<AddOrUpdateResult> AddOrUpdateAsync(
        BatchLine line,
        List<BatchCartItem> currentItems,
        List<ProductRow> rows,
        List<ProductToPartDto>? addParts,
        int? draftBatchId,
        bool forceNewDraft,
        DraftProductInfo? productInfo)
    {
        var hasChildren = addParts != null &&
                  addParts.Any(p => p.IsSelected && p.Qty > 0);

if (line.Planned <= 0 && !hasChildren)
        {
            return new AddOrUpdateResult
            {
                Success = false
            };
        }
        

        if (line.Planned < 0)
        {
            line.Planned = 0;
        }

        var rowData = rows.FirstOrDefault(r => r.VersionId == line.VersionId);

        var selectedParts = addParts?
            .Where(p => p.IsSelected && p.Qty > 0)
            .ToList()
            ?? new List<ProductToPartDto>();

        if (!draftBatchId.HasValue || forceNewDraft)
            {
                var batchId = await CreateDraftAsync(
                    line,
                    selectedParts
                );

                if (batchId == null)
                {
                    return new AddOrUpdateResult
                    {
                        Success = false
                    };
                }

                draftBatchId = batchId.Value;
            }
        
        else
            {
                var ok = await UpdateDraftAsync(
                    line,
                    currentItems,
                    selectedParts,
                    draftBatchId.Value
                );

                if (!ok)
                {
                    return new AddOrUpdateResult
                    {
                        Success = false
                    };
                }
            }

        return new AddOrUpdateResult
                {
                    Success = true,
                    BatchId = draftBatchId,
                    Items = BuildBatchItems(
                        line,
                        currentItems,
                        selectedParts,
                        rowData,
                        productInfo
                    )
                };
    }

private async Task<int?> CreateDraftAsync(
    BatchLine line,
    List<ProductToPartDto> selectedParts)
{
    var items = new List<CreateDraftItemDto>();

    if (line.Planned > 0)
    {
        items.Add(new CreateDraftItemDto
        {
            VersionId = line.VersionId,
            ProductToPartId = null,
            Qty = line.Planned,
            Comment = line.Comment
        });
    }

    foreach (var part in selectedParts)
    {
        items.Add(new CreateDraftItemDto
        {
            VersionId = line.VersionId,
            ProductToPartId = part.Id,
            Qty = part.Qty,
            Comment = line.Comment
        });
    }

    var createDto = new CreateDraftRequestDto
    {
        BatchId = null,
        Title = "",
        Comment = "Melnraksts",
        Items = items
    };

    return await _planningLookupService.CreateDraftAsync(createDto);
}

private List<BatchCartItem> BuildBatchItems(
    BatchLine line,
    List<BatchCartItem> currentItems,
    List<ProductToPartDto> selectedParts,
    ProductRow? rowData,
    DraftProductInfo? productInfo)
{
    var items = currentItems
    .Where(x => x.VersionId != line.VersionId)
    .ToList();

if (line.Planned > 0)
{
    
    items.Add(new BatchCartItem
    {
        VersionId = line.VersionId,
        Name = productInfo?.ProductName ?? "",
        VersionName = productInfo?.VersionName,
        Code = productInfo?.ProductCode ?? line.BatchCode,
        IsArchivedVersion = productInfo?.IsArchivedVersion == true,
        Qty = line.Planned,
        Comment = line.Comment,
        ProductToPartId = null,
        CategoryId = rowData?.CategoryId,
        ParentCategoryId = rowData?.ParentCategoryId
    });
}

foreach (var part in selectedParts)
{
    items.Add(new BatchCartItem
    {
        VersionId = line.VersionId,
        VersionName = productInfo?.VersionName,
        Name = part.TopPart_Name,
        Code = "",
        IsArchivedVersion = productInfo?.IsArchivedVersion == true,
        Qty = part.Qty,
        Comment = line.Comment,
        ProductToPartId = part.Id,
        CategoryId = rowData?.CategoryId,
        ParentCategoryId = rowData?.ParentCategoryId
    });
}

return items;
}

private async Task<bool> UpdateDraftAsync(
    BatchLine line,
    List<BatchCartItem> currentItems,
    List<ProductToPartDto> selectedParts,
    int draftBatchId)
{
    var dto = new DraftUpdateRequestDto
{
    BatchId = draftBatchId,
    Items = currentItems
        .Where(x => x.VersionId != line.VersionId)
        .Select(x => new DraftUpdateItemDto
        {
            VersionId = x.VersionId,
            ProductToPartId = x.ProductToPartId,
            Qty = x.Qty,
            Comment = x.Comment
        })
        .ToList()
};

if (line.Planned > 0)
{
    dto.Items.Add(new DraftUpdateItemDto
    {
        VersionId = line.VersionId,
        ProductToPartId = null,
        Qty = line.Planned,
        Comment = line.Comment
    });
}

foreach (var part in selectedParts)
{
    dto.Items.Add(new DraftUpdateItemDto
    {
        VersionId = line.VersionId,
        ProductToPartId = part.Id,
        Qty = part.Qty,
        Comment = line.Comment
    });
}

return await _planningLookupService.SaveDraftAsync(dto);
}

public async Task<DraftLoadResult?> LoadDraftAsync(
    List<ProductRow> rows)
{
    var resp = await _http.GetAsync(
    "http://localhost:5270/api/Batches/draft/last");

if (!resp.IsSuccessStatusCode ||
    resp.Content.Headers.ContentLength == 0)
{
    return null;
}

var draft = await resp.Content.ReadFromJsonAsync<DraftDto>();

if (draft == null)
{
    return null;
}

var items = new List<BatchCartItem>();

foreach (var it in draft.Items)
{
    var row = rows.FirstOrDefault(r =>
        r.VersionId == it.VersionId &&
        !r.IsRalRow);

    var isArchivedVersion =
    !it.VersionIsActive;

        if (row == null)
            {
                row = rows.FirstOrDefault(r =>
                    r.VersionId == it.VersionId &&
                    !r.IsRalRow &&
                    !string.IsNullOrWhiteSpace(r.rootName));
            }

    int? categoryId = row?.CategoryId;
    int? parentCategoryId = row?.ParentCategoryId;

    if (categoryId == null && it.ProductToPartId != null)
        {
            categoryId = 1;
            parentCategoryId = 1;
        }
        
    if (it.ProductToPartId == null && it.Qty > 0)
        {
            items.Add(new BatchCartItem
            {
                VersionId = it.VersionId,
                Code = row?.productCode ?? "",
                Name = row?.productName ?? "",
                VersionName = row?.versionName,
                IsArchivedVersion = isArchivedVersion,
                Qty = it.Qty,
                Comment = it.Comment,
                ProductToPartId = null,
                CategoryId = categoryId,
                ParentCategoryId = parentCategoryId
            });
        }
    else
        {
            items.Add(new BatchCartItem
            {
                VersionId = it.VersionId,
                VersionName = row?.versionName,
                Code = "",
                Name = $"Detaļa #{it.ProductToPartId}",
                IsArchivedVersion = isArchivedVersion,
                Qty = it.Qty,
                Comment = it.Comment,
                ProductToPartId = it.ProductToPartId,
                CategoryId = categoryId,
                ParentCategoryId = parentCategoryId
            });
        }
}

return new DraftLoadResult
{
    BatchId = draft.BatchId,
    BatchStatus = draft.BatchStatus,
    CreatedAt = draft.CreatedAt,
    Comment = draft.Comment,
    Items = items
};
}

public async Task<OpenAddDialogResult> BuildAddDialogAsync(
    PlanningGroupDto row,
    List<BatchCartItem> currentItems,
    int? draftBatchId,
    Func<int, Task<List<ProductToPartDto>>> loadParts,
    Func<int, Task<ValidateResult?>> validate)
{
    var activeVersion = row.Versions?
    .Where(v => v.VersionId.HasValue)
    .OrderByDescending(v => v.VersionId)
    .FirstOrDefault();

        if (activeVersion is null)
        {
            return new OpenAddDialogResult
            {
                Success = false
            };
        }

    if (currentItems.Any(x => x.VersionId == activeVersion.VersionId))
    {
        return new OpenAddDialogResult
        {
            Success = false
        };
    }

    var validation = await validate(activeVersion.VersionId!.Value);

    if (validation is null || !validation.isValid)
    {
        return new OpenAddDialogResult
        {
            Success = false,
            ErrorMessage = validation?.message ?? "Kļūda validācijā"
        };
    }

    var parts = await loadParts(activeVersion.VersionId.Value);
    
    var productRalRows = (row.Versions ?? new List<ProductRowDto>())
            .Where(x => x.IsRalRow)
            .OrderBy(x => x.RalCode)
            .ToList();

    foreach (var p in parts)
    {
        p.IsSelected = false;
        p.Qty = 0;
    }

    return new OpenAddDialogResult
    {
        Success = true,
        ProductName = row.ProductName,
        ProductId = activeVersion.VersionId,
        Parts = parts,
        ProductRalRows = productRalRows,
        IsPartOnlyMode = false,
        Line = new BatchLine
        {
            BatchId = draftBatchId ?? 0,
            VersionId = activeVersion.VersionId.Value,
            BatchCode = activeVersion.productCode ?? "",
            Planned = 1,
            Comment = "",
            BatchStatus = 5
        }
    };
}

public async Task<DeleteBatchItemResult> DeleteBatchItemAsync(
    BatchCartItem item,
    List<BatchCartItem> currentItems,
    int? draftBatchId)
{
    if (item.ProductToPartId == null)
    {
        currentItems = currentItems
            .Where(x => x.VersionId != item.VersionId)
            .ToList();
    }
    else
    {
        currentItems = currentItems
            .Where(x =>
                !(x.VersionId == item.VersionId &&
                  x.ProductToPartId == item.ProductToPartId))
            .ToList();
    }

    var dto = new DraftUpdateRequestDto
    {
        BatchId = draftBatchId,
        Items = currentItems.Select(x => new DraftUpdateItemDto
        {
            VersionId = x.VersionId,
            ProductToPartId = x.ProductToPartId,
            Qty = x.Qty,
            Comment = x.Comment
        }).ToList()
    };

    var ok = await _planningLookupService.SaveDraftAsync(dto);

    return new DeleteBatchItemResult
    {
        Success = ok,
        Items = currentItems
    };
    
}



}



public class AddOrUpdateResult
{
    public bool Success { get; set; }

    public int? BatchId { get; set; }

    public List<BatchCartItem> Items { get; set; } = new();
}

public class DraftProductInfo
{
    public string ProductName { get; set; } = "";
    public string? VersionName { get; set; }
    public string ProductCode { get; set; } = "";
    public bool IsArchivedVersion { get; set; }
}

public class DraftLoadResult
{
    public int? BatchId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int BatchStatus { get; set; }
    public string? Comment { get; set; }

    public List<BatchCartItem> Items { get; set; } = new();
}

public sealed class DraftDto
{
    public int BatchId { get; set; }

    public string? Comment { get; set; }
    public int BatchStatus { get; set; }

    public DateTime? CreatedAt { get; set; }

    public List<DraftUpdateItemDto> Items { get; set; } = new();
}

public class OpenAddDialogResult
{
    public bool Success { get; set; }

    public string? ProductName { get; set; }

    public int? ProductId { get; set; }

    public List<ProductToPartDto> Parts { get; set; } = new();

    public BatchLine? Line { get; set; }

    public bool IsPartOnlyMode { get; set; }

    public string? ErrorMessage { get; set; }
    public List<ProductRowDto> ProductRalRows { get; set; } = new();
}

public class ValidateResult
{
    public bool isValid { get; set; }

    public string? message { get; set; }
}

public class DeleteBatchItemResult
{
    public bool Success { get; set; }

    public List<BatchCartItem> Items { get; set; } = new();
}