KONTROLE lai netiti pievienots batch izveides melnrakstā:
- produktu versijas bez topproduct
- produkti, kuros ir kaut viens topproduct (checkbox=true) bez soļu secības, neskaitot, ja pārējiem topproduct ir pievienoti tehnoloģijas soļi

ja tiek pievienots plānā šāds produkts:
1) tiek veikta pārbaude iepr.minētiem nosacījumiem
2) izmet brīdinājumu - Ka nevar pievienot, jo nav pievienota visa informācija
3) aizveras brīdinājums
4) pie plāna melnraksta netiek pievienota prece

Šobrīd esošais kods:
- nelaiž "cauri" ja nav pievienots neviens topproudct
- bet laiž "cauri", ja ir prece atzīmēts tukšs topproducts (nav soļu) 

Ir jālabo noteikums API pusē, ka nevar izlaist cauri "slikti sagatavotu preces tehnoloģiju"

Kodi:

a) @page "/planning"
@inject HttpClient Http
@using System.Net.Http.Json
@using System.Linq
@using System.Text.Json.Serialization
@using Syncfusion.Blazor.Grids
@inject IJSRuntime JS
@using ManaApp.Shared

@using ManaApp.Models
@using ManaApp.Components

<div class="planning-page-root">

  <div class="planning-header-card">  

    <div class="planning-content">
                           <!-- Šeit sākas preču saraksts -->
        <div class="planning-main">
           
           <div class="planning-panel planning-main-block">
            <div class="planning-section-header">
                Ražošanas plānošana
            </div>

<PlanningToolbar
    SearchText="@searchText"
    SearchTextChanged="(v) => searchText = v"
    SelectedView="@selectedView"
    SelectedViewChanged="(v) => selectedView = v"
    OnDownloadExcel="DownloadPlanningExcel" />

@* <PlanningGrid Rows="@groupedRows" /> *@

<PlanningTable
    Groups="@FilteredRows"
    PlannedPlus="OnPlannedPlus"
    DraftVersionIds="batchItems.Select(x => x.VersionId).ToList()" />

           @if (selected is not null && Summary is not null)
           {
               <div style="margin-top:8px;">
                   <b>@selected.categoryName • @selected.productName (@selected.productCode)</b>
                   <div style="display:grid;grid-template-columns:200px repeat(7,110px);margin-top:6px;">
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>VersionId</b></div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>Planned</b></div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>Detailed</b></div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>Assembly</b></div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>Finishing</b></div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>Stock</b></div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>Scrap</b></div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center"><b>Out</b></div>

                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center">@selected.VersionId</div>
                       
                       @{
                           var planned = selected.Planned;
                           var bg = planned > 0 ? "#00ff00" : "transparent";
                       }
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center; background:@bg">
                           @planned
                       </div>

                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center; background:#f9b115">@Summary.Detailed</div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center; background:#f9b115">@Summary.Assembly</div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center; background:#ffe873">@Summary.Finishing</div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center; background:#efefef">@Summary.Stock</div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center; background:#efefef">@Summary.Scrap</div>
                       <div style="min-width:110px;padding:6px 10px;border:1px solid #ccc;text-align:center; background:#efefef">@Summary.Out</div>
                   </div>
               </div>
           }

        </div>
        </div>
        <!-- LABĀ PUSE: jaunais Batch panelis -->

<div class="planning-right">

    <div class="planning-section-header">
        Jauns ražošanas plāns
    </div>

    <div class="planning-section-body">
        <PlanningBatchPanel
            Items="batchItems"
            SalesItems="salesItems"
            BatchId="draftBatchId"
            OnSold="OnSalesSold"
            EditSalesRequested="OpenEditSalesPopup"
            @ref="batchPanelRef"
            OnCreateConfirmed="CreatePlanAsync"
            OnDeleteDraft="DeleteDraftAsync" />
    </div>

</div>
    
</div>

</div>

</div>

<BatchLineEditDialog
    @bind-IsOpen="isAddOpen"
    Line="addLine"
    IsAddMode="true"
    ProductName="@addProductName"
    OnSaved="OnAddSaved" />

<BatchLineEditDialog
    @bind-IsOpen="isEditOpen"
    Line="editLine"
    IsAddMode="false"
    ProductName="@editProductName"
    OnSaved="OnEditSaved" />



@*
@if (isMinusOpen && minusRow is not null)
{
    <SalesMinusDialog
        ProductName="@minusRow.productName"
        VersionName="@minusRow.versionName"
        MaxQty="@minusRow.InStock"
        OnConfirm="OnMinusConfirmed"
        OnCancel="CloseMinusDialog" />
} *@

@if (isSellPopupOpen)
{
<SalesAllocateDialog
    ProductId="sellProductId"
    ProductName="@sellProductName"
    VersionName="@sellVersionName"
    Batches="sellBatches"
    OnConfirm="@( (ManaApp.Models.SalesAllocateResult r) => OnSellPopupConfirmed(r) )"
    OnCancel="CloseSellPopup" />

}

@if (isEditSalesPopupOpen)
{
    <SalesEditDialog
    Items="editSalesItems"
    OnClose="CloseEditSalesPopup"
    OnSave="OnEditSalesSaved" />

}



@code {
   
    private List<ProductRow> rows = new();
    private List<ProductRow> allRows = new();
private List<CategoryRow> categories = new();

private List<CategoryRow> categoriesFiltered = new();

private List<SalesAllocateDialog.BatchRow> sellBatches = new();
   
private string? addProductName;
private string? editProductName;

// EDIT dialoga stāvoklis
private bool isEditOpen = false;

// rinda, ko labo
private ProductionTasks.BatchLine? editLine;

// nosaukums edit dialogam

private async Task OnEditSaved(ProductionTasks.BatchLine updated)
{
    if (updated is null)
        return;

    // atrodam rindu panelī
    var idx = batchItems.FindIndex(x => x.VersionId == updated.VersionId);
    if (idx >= 0)
    {
        batchItems[idx].Qty = updated.Planned;
        batchItems[idx].Comment = updated.Comment;
    }

    // TODO (vēlāk): draft/update API

    isEditOpen = false;
    StateHasChanged();
}



    private int? draftBatchId = null;
private bool isCreatingDraft = false;

private string? createPlanError;

private bool isInitializing = true;

protected override async Task OnInitializedAsync()
{
    await ReloadPlanningDataAsync();
    await LoadDraftAsync();
    await LoadSalesDraftAsync();

    isInitializing = false;
}

private async Task LoadSummariesForRows(IEnumerable<ProductRow> targetRows)
    {
        var ids = targetRows
            .Where(x => x.VersionId.HasValue && x.VersionId > 0)
            .Select(x => x.VersionId!.Value)
            .Distinct()
            .ToList();

        var summaries = await Http.GetFromJsonAsync<List<StockSummary>>(
            $"http://localhost:5270/api/stockmovements/summary-multi?ids={string.Join(",", ids)}");

var map = summaries?.ToDictionary(x => x.VersionId) ?? new();
        foreach (var r in targetRows)
        {
    
        if (r.VersionId is null || r.VersionId <= 0)
            continue;

if (r.VersionId is not int vid || !map.TryGetValue(vid, out var sum))
    continue;

        r.InStock = sum.Stock;

        // ⬅️ ŠĪ IR TRŪKSTOŠĀ RINDA
        r.AssemblyFinish = sum.Assembly;

        r.FinishingInProgress = sum.Finishing;
        r.InStock            = sum.Stock;


var finAlloc = await Http.GetFromJsonAsync<FinAllocatedDto>(
    $"http://localhost:5270/api/tasks/finishing-allocated-by-version?versionId={r.VersionId}");

r.FinishingAllocated = finAlloc?.FinishingAllocated ?? 0;

var finishingRal =
    await Http.GetFromJsonAsync<List<FinishingRalRow>>(
        $"http://localhost:5270/api/tasks/finishing-by-version-ral?versionId={r.VersionId}");

if (finishingRal != null)
{
    r.FinishingRal = finishingRal;
}

        }

        // tikai vienreiz pēc cikla
        StateHasChanged();
    }

private void BuildCategoriesFromRows(IEnumerable<ProductRow> source)
{
    categories = source
        .Where(r => !string.IsNullOrWhiteSpace(r.categoryName))
        .Select(r => (r.categoryName ?? "").Trim())
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(name => name)
        .Select(name => new CategoryRow { CategoryName = name })
        .ToList();
        // 0. līmeņa datu avots tree-view gridam
    categoriesFiltered = categories;
}

    private string? draftComment;

    private List<ProductSimpleRow> simpleRows = new();
    private string searchText = "";
    private ProductViewType selectedView = ProductViewType.Kauss;
    private int VersionId = 1;

    private StockSummary? Summary;
    private string? Raw;        
    private string? Error;      

   private async Task Load()
{
    Error = null;
    Summary = null;
    Raw = null;

    if (selected is null || !(selected.VersionId.HasValue && selected.VersionId > 0))
{
    Error = "Nav izvēlēta rinda vai nav VersionId.";
    return;
}

    try
    {
        var url = $"http://localhost:5270/api/stockmovements/summary?versionId={selected.VersionId}";
        Console.WriteLine($"CLICK Load -> {url}");
        
        Raw = await Http.GetStringAsync(url);
        Console.WriteLine($"RAW LEN = {Raw?.Length}");

        if (string.IsNullOrWhiteSpace(Raw)) { Error = "Empty response"; return; }
        Summary = System.Text.Json.JsonSerializer.Deserialize<StockSummary>(Raw)!;

        Console.WriteLine($"Parsed Finishing = {Summary?.Finishing}");
    }
    catch (Exception ex)
    {
        Error = ex.ToString();
        Console.WriteLine(Error);
    }
}

    
    private ProductRow? selected;

    private async Task OnRowSelected(RowSelectEventArgs<ProductRow> args)
{
    selected = args.Data;
    VersionId = selected?.VersionId ?? 0; // var paturēt tikai info dēļ
    await Load();                         // vienmēr lādējam pēc BatchProduct_ID
}

private async Task ReloadAfterCreate()
{
    await ReloadPlanningDataAsync();
}


private IEnumerable<ProductRow> GetRowsForCategory(object catObj)
{
    var cat = catObj as CategoryRow;
    if (cat is null) return Enumerable.Empty<ProductRow>();

    var key = (cat.CategoryName ?? "").Trim();

    // Pamatā – tikai šīs kategorijas preces
    var query = rows.Where(r =>
        string.Equals((r.categoryName ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase)
    );

    // Ja nav meklēšanas teksta – atdod visus šīs kategorijas produktus
    if (string.IsNullOrWhiteSpace(searchText))
        return query;

    // Ja ir meklēšana – filtrē šīs kategorijas iekšienē
    return query.Where(r =>
        (r.productName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (r.productCode?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (r.categoryName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
    );
}

private async Task ReloadPlanningDataAsync()
{
    // 1) Produkti
    rows = await Http.GetFromJsonAsync<List<ProductRow>>(
    "http://localhost:5270/api/products/planning-list")
    ?? new();

    allRows = new List<ProductRow>(rows);

    // 2) Stock + FinishingInProgress
    await LoadSummariesForRows(rows);

    // 3) Batch dati (Planned / Detailed / Assembly)
    var plannedList = await Http.GetFromJsonAsync<List<ManaApp.Models.BatchPlannedRow>>(

        "http://localhost:5270/api/batches/list?batch_type=1")
        ?? new();

    var plannedByVersion = plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.Planned));

    var detailedInProgressByVersion = plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.DetailedInProgress));

    var detailedFinishByVersion = plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.DetailedFinish));

    var assemblyInProgressByVersion = plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.AssemblyInProgress));

    var assemblyFinishByVersion = plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.AssemblyFinish));

// ⬇ atņemam pārdoto ASSEMBLY no UI (pēc commit)
    var soldAssemblyByVersion = salesItems
        .Where(x => x.IsAssembly)
        .GroupBy(x => x.VersionId)
        .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));


    // 4) Aizpildām rindas
    foreach (var r in rows)
    {
        if (!r.VersionId.HasValue)
        {
            r.Planned = 0;
            r.DetailedInProgress = 0;
            r.DetailedFinish = 0;
            r.AssemblyINProgress = 0;
            r.AssemblyFinish = 0;
            continue;
        }

        var vid = r.VersionId.Value;

        r.Planned = plannedByVersion.TryGetValue(vid, out var p) ? p : 0;
        r.DetailedInProgress = detailedInProgressByVersion.TryGetValue(vid, out var d) ? d : 0;
        r.DetailedFinish = detailedFinishByVersion.TryGetValue(vid, out var df) ? df : 0;
        r.AssemblyINProgress = assemblyInProgressByVersion.TryGetValue(vid, out var ap) ? ap : 0;
            // Asm.F = reālais Assembly no stock_movements
            // LoadSummariesForRows() jau ielika šo vērtību  

            if (r.AssemblyFinish < 0)
                r.AssemblyFinish = 0;

    }

    // 5) Sakārtojam
    rows = rows
        .OrderBy(r => r.categoryName)
        .ThenBy(r => r.productName)
        .ToList();


// Uztaisām "viena rinda uz produktu" + bērni = versijas
// Uztaisām "viena rinda uz produktu" + bērni = versijas
groupedRows = rows
    .GroupBy(r =>
        $"{(r.categoryName ?? "").Trim()}||{(r.productCode ?? "").Trim()}",
        StringComparer.OrdinalIgnoreCase)
    .Select(g =>
    {
        var parts = g.Key.Split("||", 2);
        var cat  = parts.Length > 0 ? parts[0] : "";
        var code = parts.Length > 1 ? parts[1] : "";

        var first = g.First();

  return new ProductGroupRow
        {
            CategoryName = cat,
            ProductCode  = code,
            ProductName  = first.productName ?? "",

            InStock = g
                .Where(x => !x.IsRalRow)
                .Sum(x => x.InStock),
            Planned = g.Sum(x => x.Planned),
            DetailedInProgress = g.Sum(x => x.DetailedInProgress),
            DetailedFinish     = g.Sum(x => x.DetailedFinish),
            AssemblyINProgress = g
                .Where(x => !x.IsRalRow)
                .Sum(x => x.AssemblyINProgress),
            AssemblyFinish = g
                .Where(x => !x.IsRalRow)
                .Sum(x => x.AssemblyFinish),
            FinishingInProgress = g
                .Where(x => !x.IsRalRow)
                .Sum(x => x.FinishingInProgress),
            Versions = g
                .OrderByDescending(x => x.VersionId ?? 0)
                .SelectMany(v =>
                {
                    var list = new List<ProductRow>();

                    if (!v.IsRalRow)
                    {
                        v.InStock = 0;
                        v.FinishingInProgress = 0;
                        list.Add(v);
                    }

                    if (v.FinishingRal != null && v.FinishingRal.Count > 0)
                    {
                        foreach (var ral in v.FinishingRal)
                        {
                            list.Add(new ProductRow
                            {
                                productName = v.productName,
                                productCode = v.productCode,
                                versionName = v.versionName,
                                RalCode = ral.RalCode,
                                FinishingInProgress = ral.Status == 3 ? 0 : ral.Qty,
                                InStock = ral.Status == 3 ? ral.Qty : 0,
                                FinishingStatus = ral.Status,
                                IsRalRow = true
                            });
                        }
                    }

                    return list;
                })
            .ToList()
        };
    })
    
    .OrderBy(x => x.CategoryName)
    .ThenBy(x => x.ProductName)
    .ToList();

flatRows.Clear();

foreach (var grp in groupedRows
    .GroupBy(g => g.CategoryName)
    .OrderBy(g => g.Key))
{
    flatRows.Add(new CategoryHeaderRow
    {
        CategoryName = grp.Key
    });

    foreach (var row in grp.OrderBy(r => r.ProductName))
    {
        flatRows.Add(row);
    }
}


    BuildLatestVersionByCode();
    await InvokeAsync(StateHasChanged);
}

public sealed class FinProgressDto
{
    [JsonPropertyName("finishingInProgress")]
    public int FinishingInProgress { get; set; }
}


private async Task DownloadPlanningExcel()
{
    // atver jaunu tabu un lejupielādē failu no API
    await JS.InvokeVoidAsync("open", "http://localhost:5270/api/reports/planning-excel", "_blank");
}

// productCode -> latest VersionId
private readonly Dictionary<string, int> _latestVersionByCode = new(StringComparer.OrdinalIgnoreCase);

private void BuildLatestVersionByCode()
{
    _latestVersionByCode.Clear();

    foreach (var g in rows
        .Where(r => !string.IsNullOrWhiteSpace(r.productCode) && r.VersionId is > 0)
        .GroupBy(r => r.productCode.Trim(), StringComparer.OrdinalIgnoreCase))
    {
        _latestVersionByCode[g.Key] = g.Max(x => x.VersionId!.Value);
    }
}
private bool IsLatestVersion(ProductRow r)
{
    if (r is null) return false;
    if (string.IsNullOrWhiteSpace(r.productCode)) return true; // ja nav koda, lai netraucē
    if (r.VersionId is not int v || v <= 0) return false;

    return _latestVersionByCode.TryGetValue(r.productCode.Trim(), out var latest) && v == latest;
}

private List<ProductGroupRow> groupedRows = new();

private List<IPlanningRow> flatRows = new();

private List<ProductGroupRow> FilteredRows =>
    groupedRows
        .Where(MatchRoot)
        .Where(g =>
            string.IsNullOrWhiteSpace(searchText) ||
            g.ProductName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            g.ProductCode.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            g.CategoryName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            g.Versions.Any(v =>
                v.productName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true ||
                v.productCode?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true
            )
        )
        .ToList();

private PlanningBatchPanel? batchPanelRef;

private async Task OnPlannedPlus(ProductGroupRow row)
{
    if (row.Versions == null || row.Versions.Count == 0)
        return;

    var ver = row.Versions
        .FirstOrDefault(v => IsLatestVersion(v));

    if (ver is null)
        return;

    // ✅ ŠEIT ieliekam pareizo produkta nosaukumu dialogam
    addProductName = row.ProductName;

    addProductId = ver.id;

    addLine = new ProductionTasks.BatchLine
    {
        BatchId     = draftBatchId ?? 0,
        VersionId   = ver.VersionId!.Value,
        BatchCode   = ver.productCode,
        Planned     = 1,
        Comment     = "",
        BatchStatus = 5
    };

    isAddOpen = true;
}

private async Task OnAddSaved(ProductionTasks.BatchLine line)
{
    ProductContentDto? product = null;

if (addProductId.HasValue)
{
    product = await Http.GetFromJsonAsync<ProductContentDto>(
    $"http://localhost:5270/api/products/content?versionId={addProductId.Value}");
};
    
    if (line.Planned <= 0)
        return;
var rowData = rows.FirstOrDefault(r => r.VersionId == line.VersionId);

    // 1) JA NAV draftBatchId -> izveidojam batch (status 4) + ieliekam 1. produktu
if (!draftBatchId.HasValue || forceNewDraft)

{
var createDto = new
{
    BatchId = (int?)null,
    Title = "", // ⬅️ SVARĪGI: ļaujam backendam ģenerēt unikālu kodu
    Comment = "Melnraksts",
    Items = new[]
    {
        new
        {
            VersionId = line.VersionId,
            Qty       = line.Planned,
            Comment   = line.Comment
        }
    }
};


    var createResp = await Http.PostAsJsonAsync(
        "http://localhost:5270/api/batches/draft/create",
        createDto);

    if (!createResp.IsSuccessStatusCode)
            {
                var err = await createResp.Content.ReadAsStringAsync();
                await JS.InvokeVoidAsync("alert", err);
                return;
            }

    var data = await createResp.Content.ReadFromJsonAsync<Dictionary<string, int>>();
    if (data != null && data.TryGetValue("batchId", out var id))
        draftBatchId = id;
    forceNewDraft = false;


    // ⬇️ KRITISKI SVARĪGI — UI fiksē pirmo preci

        batchItems.Add(new BatchCartItem
        {
            VersionId = line.VersionId,
            Name = product?.ProductName ?? "",
            Code = product?.ProductCode ?? line.BatchCode,
            Qty = line.Planned,
            Comment = line.Comment,
            CategoryId = rowData?.CategoryId,
            ParentCategoryId = rowData?.ParentCategoryId
        });

    isAddOpen = false;
    addLine = null;
    StateHasChanged();
    return;
}
    // 2) JA draftBatchId jau ir -> update
// 2) JA draftBatchId jau ir -> update
var dto = new DraftUpdateDto
{
    BatchId = draftBatchId.Value
};

// esošās preces
foreach (var it in batchItems)
{
    dto.Items.Add(new DraftItemDto
    {
        VersionId = it.VersionId,
        Qty       = it.Qty,
        Comment   = it.Comment
    });
}

// ⬇️ KRITISKI: PIEVIENO JAM JAUNO RINDU
dto.Items.Add(new DraftItemDto
{
    VersionId = line.VersionId,
    Qty       = line.Planned,
    Comment   = line.Comment
});

var resp = await Http.PostAsJsonAsync(
    "http://localhost:5270/api/batches/draft/update",
    dto);

if (!resp.IsSuccessStatusCode)
    return;

// UI atjauninām TIKAI PĒC veiksmīga API

batchItems.Add(new BatchCartItem
{
    VersionId = line.VersionId,
    Name = product?.ProductName ?? "",
    Code = product?.ProductCode ?? line.BatchCode,
    Qty = line.Planned,
    Comment = line.Comment,
    CategoryId = rowData?.CategoryId,
    ParentCategoryId = rowData?.ParentCategoryId
});

await LoadDraftAsync();

isAddOpen = false;
addLine = null;
StateHasChanged();
}

private bool MatchRoot(ProductGroupRow g)
{
    var root = selectedView == ProductViewType.Kauss
        ? "KAUSS"
        : "ADAPTERIS";

    // skatāmies uz jebkuru versiju grupā (visām būs viens root)
    var any = g.Versions?.FirstOrDefault();
    if (any is null) return false;

    var stem = root.Length > 1 ? root[..^1] : root;
    return (any.rootName ?? "")
        .StartsWith(stem, StringComparison.OrdinalIgnoreCase);
}

private List<BatchCartItem> batchItems = new();

// popup stāvoklis
private bool isAddOpen = false;

// rinda, no kuras tiek spiests +
private ProductionTasks.BatchLine? addLine;


private sealed class DraftCreateDto
{
    public string Title { get; set; } = "";
    public string? Comment { get; set; }
}

private sealed class DraftDto
{
    public int BatchId { get; set; }
    public string? Comment { get; set; }
    public List<DraftItemDto> Items { get; set; } = new();
}

private sealed class DraftItemDto
{
    public int VersionId { get; set; }
    public int Qty { get; set; }
    public string? Comment { get; set; }
}

private sealed class DraftUpdateDto
{
    public int? BatchId { get; set; }
    public List<DraftItemDto> Items { get; set; } = new();
}

private async Task LoadDraftAsync()
{
    Console.WriteLine("LOAD DRAFT: start");

    try
    {
        var resp = await Http.GetAsync(
    "http://localhost:5270/api/Batches/draft/last"
);

if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentLength == 0)
{

    return;
}

var draft = await resp.Content.ReadFromJsonAsync<DraftDto>();
if (draft == null)
    return;


        if (draft == null)
        {
            Console.WriteLine("LOAD DRAFT: draft == null");
            return;
        }

        
        // ✅ 1) saglabājam melnraksta ID
        draftBatchId = draft.BatchId;
draftComment = draft.Comment;

batchItems.Clear();

foreach (var it in draft.Items)
{
    var row = rows.FirstOrDefault(r => r.VersionId == it.VersionId);

                batchItems.Add(new BatchCartItem
                    {
                        VersionId = it.VersionId,
                        Code      = row?.productCode ?? "",
                        Name      = row?.productName ?? "",
                        Qty       = it.Qty,
                        Comment   = it.Comment,
                        CategoryId = row?.CategoryId,
                        ParentCategoryId = row?.ParentCategoryId
                    });

}


        batchPanelRef?.SetComment(draft.Comment);
        StateHasChanged();
    }
    catch (Exception ex)
    {
        Console.WriteLine("LOAD DRAFT ERROR: " + ex.Message);
    }
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender)
        return;

    if (!string.IsNullOrWhiteSpace(draftComment))
    {
        batchPanelRef?.SetComment(draftComment);
    }
}

private async Task<bool> CreatePlanAsync(string planCode)
{
if (!draftBatchId.HasValue)
    return false;


    // 1️⃣ unikuma pārbaude
    var check = await Http.GetAsync(
        $"http://localhost:5270/api/batches/check-code?code={Uri.EscapeDataString(planCode)}");

    if (!check.IsSuccessStatusCode)
    {
        // ❗ atgriežam kļūdu UZ PANELI
        batchPanelRef?.SetCreateError("Šāds ražošanas plāna kods jau eksistē.");
        return false;
    }

    // 2️⃣ status + code
    var resp = await Http.PostAsJsonAsync(
        "http://localhost:5270/api/batches/planned",
        new
        {
            batchId = draftBatchId.Value,
            code = planCode
        });

    if (!resp.IsSuccessStatusCode)
    {
        batchPanelRef?.SetCreateError("Neizdevās izveidot ražošanas plānu.");
        return false;
    }

    // 3️⃣ viss OK → RESET
    draftBatchId = null;
    batchItems.Clear();
    draftComment = null;
    forceNewDraft = true;

batchPanelRef?.CloseCreateDialog();
await ReloadPlanningDataAsync();
await InvokeAsync(StateHasChanged);
return true;

}

private bool forceNewDraft = false;

private async Task DeleteDraftAsync()
{
    if (!draftBatchId.HasValue)
        return;

    var resp = await Http.PostAsJsonAsync(
        "http://localhost:5270/api/batches/draft/delete",
        new { batchId = draftBatchId.Value }
    );

    if (!resp.IsSuccessStatusCode)
    {
        await JS.InvokeVoidAsync(
            "alert",
            "Neizdevās dzēst melnrakstu"
        );
        return;
    }

    // UI reset
    draftBatchId = null;
    batchItems.Clear();
    draftComment = null;

    forceNewDraft = true;

    batchPanelRef?.SetComment(null);
    batchPanelRef?.Refresh();

    await ReloadPlanningDataAsync();   
    StateHasChanged();
}


private List<SalesCartItem> salesItems = new();

// popup rediģējamā kopija (Edit-Copy pattern)
private List<SalesCartItem> editSalesItems = new();



// pievienots pārdošanas console

private async Task OnSalesSold()
{
    var ok = await JS.InvokeAsync<bool>(
        "confirm",
        "Apstiprināt pārdošanu?"
    );

    if (!ok)
        return;

    var resp = await Http.PostAsync(
        "http://localhost:5270/api/sales-drafts/commit",
        null
    );

    if (!resp.IsSuccessStatusCode)
    {
        var err = await resp.Content.ReadAsStringAsync();
        await JS.InvokeVoidAsync("alert", err);
        return;
    }

    // UI RESET pēc commit
    salesItems.Clear();
    await ReloadPlanningDataAsync();
    batchPanelRef?.Refresh();
    StateHasChanged();
}

public class SalesCommitResult
{
    public bool Ok { get; set; }
    public List<SalesCommitItem> Items { get; set; } = new();
}

public class SalesCommitItem
{
    public int VersionId { get; set; }
    public int InStock { get; set; }
}

// 10.01.2026 labojums JAUNAJAM Pārdošanas popup logam:

private bool isSellPopupOpen = false;
private int sellProductId;
private string? sellProductName;
private string? sellVersionName;
private int? sellVersionId;

private SalesSummary? pendingSale;

//testa labojums 10.01.2026

private async Task OpenSellPopup(ProductRow row)
{
    sellProductId = row.id;
    sellProductName = row.productName ?? "";
    sellVersionName = row.versionName;
    sellVersionId = row.VersionId;

    sellBatches.Clear();

    var apiRows = await Http.GetFromJsonAsync<List<ApiBatchRow>>(
        $"http://localhost:5270/api/stockmovements/available-by-batch?versionId={row.VersionId}");

    if (apiRows != null)
    {
        sellBatches = apiRows
            .GroupBy(x => new { x.BatchProductId, x.BatchCode })
            .Select(g => new SalesAllocateDialog.BatchRow
            {
                BatchProductId = g.Key.BatchProductId,
                BatchCode = g.Key.BatchCode,
                StockQty = g.Where(x => x.MoveType == "STOCK").Sum(x => x.AvailableQty),
                AssemblyQty = g.Where(x => x.MoveType == "ASSEMBLY").Sum(x => x.AvailableQty)
            })
            .ToList();
    }
    

    isSellPopupOpen = true;
    StateHasChanged();
}

private sealed class ApiBatchRow
{
    public int BatchProductId { get; set; }
    public string BatchCode { get; set; } = "";
    public string MoveType { get; set; } = "";
    public int AvailableQty { get; set; }
}


private void CloseSellPopup()
{
    isSellPopupOpen = false;
    sellVersionId = null;
}

// pārdošana Ok apstirpināšanai
public sealed class StockMoveDto
{
    public int Version_ID { get; set; }
    public string From { get; set; } = "";   // "STOCK" | "ASSEMBLY"
    public string To { get; set; } = "OUT";
    public int Qty { get; set; }
    public int BatchProduct_ID { get; set; }
}

private async Task OnSellPopupConfirmed(SalesAllocateResult result)
{
    var versionId = sellVersionId ?? 0;
    var selections = result.BatchSelections ?? new List<BatchSelection>();

    salesItems.RemoveAll(it => it.VersionId == versionId);

// STRICT DATA COMPLETION
// SalesBatchItem already has BatchCode property
// Copy BatchCode directly from selection (s.BatchCode)
// Do NOT change existing logic, filters, or structure
// Do NOT add fallbacks or new logic

var stockBatches = selections
    .Where(s => s.FromStock > 0)
    .Select(s =>
    {
        var avail = sellBatches
            .FirstOrDefault(b => b.BatchProductId == s.BatchProductId);

        return new SalesBatchItem
        {
            BatchProductId = s.BatchProductId,
            BatchCode      = s.BatchCode,
            Qty            = s.FromStock,
            AvailableQty   = avail?.StockQty ?? 0
        };
    })
    .ToList();


    if (stockBatches.Count > 0)
    {
        salesItems.Add(new SalesCartItem
        {
            VersionId = versionId,
            ProductName = result.ProductName,
            VersionName = sellVersionName,
            Qty = stockBatches.Sum(b => b.Qty),
            InStock = result.StockTotal,
            IsAssembly = false,
            Batches = stockBatches
        });
    }

var assemblyBatches = selections
    .Where(s => s.FromAssembly > 0)
    .Select(s =>
    {
        var avail = sellBatches
            .FirstOrDefault(b => b.BatchProductId == s.BatchProductId);

        return new SalesBatchItem
        {
            BatchProductId = s.BatchProductId,
            BatchCode      = s.BatchCode,
            Qty            = s.FromAssembly,
            AvailableQty   = avail?.AssemblyQty ?? 0
        };
    })
    .ToList();


    if (assemblyBatches.Count > 0)
    {
        salesItems.Add(new SalesCartItem
        {
            VersionId = versionId,
            ProductName = result.ProductName,
            VersionName = sellVersionName,
            Qty = assemblyBatches.Sum(b => b.Qty),
            InStock = result.AssemblyTotal,
            IsAssembly = true,
            Batches = assemblyBatches
        });
    }

    isSellPopupOpen = false;
sellVersionId = null;

await SaveSalesDraftAsync();     // 1️⃣ saglabā draftu
await LoadSalesDraftAsync();     // 2️⃣ atjauno salesItems no DB
await ReloadPlanningDataAsync(); // 3️⃣ pārrēķina ASM.F


batchPanelRef?.Refresh();
await SaveSalesDraftAsync();


}



private void OpenEditSalesPopup()
{
    // Izveidojam dziļu salesItems kopiju popup rediģēšanai
    editSalesItems = salesItems

    // STRICT COPY FIX
// This is a deep copy for edit popup
// Copy ALL existing SalesBatchItem properties AS-IS
// Do NOT rename properties
// Do NOT add logic
// Do NOT change structure

        .Select(item => new SalesCartItem
        {
            VersionId   = item.VersionId,
            ProductName = item.ProductName,
            VersionName = item.VersionName,
            Qty         = item.Qty,
            InStock     = item.InStock,
            IsAssembly  = item.IsAssembly,

            Batches = item.Batches
            .Select(b => new SalesBatchItem
            {
                BatchProductId = b.BatchProductId,
                BatchCode      = b.BatchCode,
                Qty            = b.Qty,
                AvailableQty   = b.AvailableQty   // ⬅️ Svarīgi, lai parādās apjoms pie popupā
            })
            .ToList()
        })
        .ToList();

    isEditSalesPopupOpen = true;
}
      
private bool isEditSalesPopupOpen = false;

private void CloseEditSalesPopup()
{
    isEditSalesPopupOpen = false;
}
private Task OnEditSalesSaved(List<SalesCartItem> updatedItems)
{
    foreach (var updated in updatedItems)
    {
        // 1) pārrēķinam summu no batchiem
        updated.Qty = updated.Batches.Sum(b => b.Qty);

        // 2) ja šim (VersionId + IsAssembly) nav ko pārdot → izņemam rindu
        if (updated.Qty <= 0)
        {
            salesItems.RemoveAll(x =>
                x.VersionId == updated.VersionId &&
                x.IsAssembly == updated.IsAssembly);
            continue;
        }

        // 3) citādi – parasts update
        var idx = salesItems.FindIndex(x =>
            x.VersionId == updated.VersionId &&
            x.IsAssembly == updated.IsAssembly);

        if (idx >= 0)
            salesItems[idx] = updated;
        else
            salesItems.Add(updated);
    }

    // 4) aizveram popup un atjaunojam UI
    isEditSalesPopupOpen = false;
    batchPanelRef?.Refresh();
    StateHasChanged();

    return Task.CompletedTask;
}

private async Task SaveSalesDraftAsync()
{
    if (isInitializing)
        return;

    var dto = new
    {
        Items = salesItems
            .SelectMany(it => it.Batches.Select(b => new
            {
                VersionId      = it.VersionId,
                BatchProductId = b.BatchProductId,
                BatchCode      = b.BatchCode,
                Qty            = b.Qty,
                IsAssembly     = it.IsAssembly
            }))
            .Where(x => x.Qty > 0)
            .ToList()
    };

    await Http.PostAsJsonAsync(
        "http://localhost:5270/api/sales-drafts/autosave",
        dto
    );
}

private async Task LoadSalesDraftAsync()
{
      var resp = await Http.GetAsync(
        "http://localhost:5270/api/sales-drafts/last"
    );

    if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentLength == 0)
        return;

    var draft = await resp.Content.ReadFromJsonAsync<SalesDraftDto>();
    if (draft == null)
        return;

    salesItems.Clear();

var grouped = draft.Items
    .GroupBy(x => new { x.VersionId, x.IsAssembly });

foreach (var g in grouped)
{
    var row = rows.FirstOrDefault(r => r.VersionId == g.Key.VersionId);

    salesItems.Add(new SalesCartItem
    {
        VersionId   = g.Key.VersionId,
        ProductName = row?.productName ?? "",
        VersionName = row?.versionName,
        IsAssembly  = g.Key.IsAssembly,

        // summa no batchiem
        Qty = g.Sum(x => x.Qty),

        InStock = row?.InStock ?? 0,

        // ⬅️ KRITISKI: batchi nāk NO DRAFTA, nevis no API
        Batches = g.Select(x => new SalesBatchItem
        {
            BatchProductId = x.BatchProductId,
            BatchCode      = x.BatchCode,
            Qty            = x.Qty,
            AvailableQty   = 0   // draft = patiesība
        }).ToList()
    });
}

// 🔹 ielādējam REĀLO pieejamo atlikumu no noliktavas
foreach (var item in salesItems)
{
    var apiRows = await Http.GetFromJsonAsync<List<ApiBatchRow>>(
        $"http://localhost:5270/api/stockmovements/available-by-batch?versionId={item.VersionId}");

    if (apiRows == null)
        continue;

    foreach (var b in item.Batches)
    {
        var api = apiRows.FirstOrDefault(x =>
            x.BatchProductId == b.BatchProductId &&
            (item.IsAssembly
                ? x.MoveType == "ASSEMBLY"
                : x.MoveType == "STOCK"));

        if (api != null)
            b.AvailableQty = api.AvailableQty;
    }
}

batchPanelRef?.Refresh();
StateHasChanged();

}

private sealed class SalesDraftDto
{
    public int DraftId { get; set; }
    public List<SalesDraftItemDto> Items { get; set; } = new();
}

private sealed class SalesDraftItemDto
{
    public int VersionId { get; set; }
    public int BatchProductId { get; set; }   // pievienots 16.01.2026
    public string BatchCode { get; set; } = ""; // pievienots 16.01.2026
    public int Qty { get; set; }
    public bool IsAssembly { get; set; }
}

private int? addProductId;


}


b) // Šis kontrolieris ir paredzēts partiju (batch) pārvaldībai: izveidei, rediģēšanai, dzēšanai, kā arī partiju saraksta un detaļas skatīšanai.

using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
using MySqlConnector;
namespace ManiApi.Controllers

{
    [ApiController]
    [Route("api/[controller]")]
    public class BatchesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public BatchesController(AppDbContext db) => _db = db;

// POST: /api/batches/draft/create
[HttpPost("draft/create")]
    public async Task<IActionResult> CreateDraft([FromBody] BatchCartModel dto)
        {
            if (dto is null) return BadRequest("Tukšs pieprasījums.");
            var code = (dto.Title ?? "").Trim();

// ✅ JA MELNRAKSTS UN NAV NOSAUKUMA → tiek ģenerēts automātiski
if (string.IsNullOrWhiteSpace(code))
{
    code = "__DRAFT__" + Guid.NewGuid().ToString("N")[..8];
}

if (dto.Items is null || dto.Items.Count == 0)
{
    return BadRequest("Nav nevienas produkta rindas.");
}

// DB savienojums + transakcija
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // ✅ Pārbaudām vai visiem produktiem ir tehnoloģija (TopParts)
if (dto.Items is not null)
{
    foreach (var it in dto.Items)
    {
// 1️⃣ Vai vispār ir TopParts
await using var cmd1 = conn.CreateCommand();
cmd1.Transaction = tx;
cmd1.CommandText = @"
SELECT COUNT(*)
FROM producttopparts
WHERE Version_ID = @vid
  AND IsActive = 1;";

var p1 = cmd1.CreateParameter();
p1.ParameterName = "@vid";
p1.Value = it.VersionId;
cmd1.Parameters.Add(p1);

var topPartsCnt = Convert.ToInt32(await cmd1.ExecuteScalarAsync());

if (topPartsCnt == 0)
{
    await tx.RollbackAsync();
    return BadRequest($"Produktam (VersionId={it.VersionId}) nav TopPart (tehnoloģijas).");
}


// 2️⃣ Vai ir vismaz viens STEP
await using var cmd2 = conn.CreateCommand();
cmd2.Transaction = tx;
cmd2.CommandText = @"
SELECT COUNT(*) 
FROM producttopparts ptp
WHERE ptp.Version_ID = @vid
  AND ptp.IsActive = 1
  AND NOT EXISTS (
      SELECT 1 
      FROM toppartsteps ts
      WHERE ts.ProductToPart_ID = ptp.ID
        AND ts.IsActive = 1
  );";

var p2 = cmd2.CreateParameter();
p2.ParameterName = "@vid";
p2.Value = it.VersionId;
cmd2.Parameters.Add(p2);

var invalidCnt = Convert.ToInt32(await cmd2.ExecuteScalarAsync());

if (invalidCnt > 0)
{
    await tx.RollbackAsync();
    return BadRequest($"Produktam (VersionId={it.VersionId}) ir izvēlēts TopPart bez STEP.");
}
    }
}

            // 1) Unikuma pārbaude (starp VISIEM statusiem)
            await using (var chk = conn.CreateCommand())
            {
                chk.Transaction = tx;
                chk.CommandText = @"SELECT COUNT(*) FROM batches WHERE Batches_Code = @code;";
                var p = chk.CreateParameter(); p.ParameterName = "@code"; p.Value = code; chk.Parameters.Add(p);
                var cnt = Convert.ToInt32(await chk.ExecuteScalarAsync());
                if (cnt > 0)
                {
                    await tx.RollbackAsync();
                    return Conflict("Nosaukums (Title) jau eksistē. Izvēlies citu.");
                }
            }

            // 2) Header INSERT (statuss = 4 – melnraksts)
            int batchId;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO batches (Batches_Code, Batches_Statuss, Batches_StartDate, Batches_EndDate, Comments, IsActive)
VALUES (@code, 4, NULL, NULL, @comment, 1);
SELECT LAST_INSERT_ID();";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@code"; p1.Value = code; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@comment"; p2.Value = (object?)dto.Comment ?? DBNull.Value; cmd.Parameters.Add(p2);

                var obj = await cmd.ExecuteScalarAsync();
                batchId = Convert.ToInt32(obj);
            }

            // 3) Rindas (UPSERT pēc (Batch_Id, Version_Id))
if (dto.Items is not null)
{
    foreach (var it in dto.Items)
    {
        await using var row = conn.CreateCommand();
        row.Transaction = tx;
    row.CommandText = @"
INSERT INTO batches_products
    (Batch_Id, Version_Id, Planned_Qty, Done_Qty, Priority, BatchProduct_Comments, IsActive)
VALUES
    (@bid, @vid, @qty, 0, 0, @comment, 1)
ON DUPLICATE KEY UPDATE
    Planned_Qty           = VALUES(Planned_Qty),
    BatchProduct_Comments = VALUES(BatchProduct_Comments),
    IsActive              = 1;";


        var pb = row.CreateParameter();
        pb.ParameterName = "@bid";
        pb.Value = batchId;
        row.Parameters.Add(pb);

        var pv = row.CreateParameter();
        pv.ParameterName = "@vid";
        pv.Value = it.VersionId;
        row.Parameters.Add(pv);

        var pq = row.CreateParameter();
        pq.ParameterName = "@qty";
        pq.Value = it.Qty;
        row.Parameters.Add(pq);

        var pc = row.CreateParameter();
        pc.ParameterName = "@comment";
        pc.Value = (object?)it.Comment ?? DBNull.Value;
        row.Parameters.Add(pc);

        await row.ExecuteNonQueryAsync();
    }
}


            await tx.CommitAsync();
            return Ok(new { batchId });
        }

[HttpGet("check-code")]
public async Task<IActionResult> CheckCode([FromQuery] string code)
{
    if (string.IsNullOrWhiteSpace(code))
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COUNT(*)
FROM batches
WHERE Batches_Code = @code
  AND IsActive = 1;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@code";
    p.Value = code;
    cmd.Parameters.Add(p);

    var cnt = Convert.ToInt32(await cmd.ExecuteScalarAsync());

    return cnt > 0 ? Conflict() : Ok();
}


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBatch(int id, [FromQuery] string? reason = null)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // 1) Atzīmējam header kā neaktīvu un iestatām “Dzēsts” statusu (5)
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE batches 
   SET IsActive = 0,
       Batches_Statuss = 5,
       Comments = CASE 
                    WHEN @reason IS NULL OR @reason = '' THEN Comments
                    ELSE CONCAT(
                           COALESCE(Comments, ''), 
                           CASE WHEN LENGTH(COALESCE(Comments,''))>0 THEN ' | ' ELSE '' END,
                           @stamp, ' – ', @reason)
                  END
 WHERE ID = @id;";
                var pId = cmd.CreateParameter(); pId.ParameterName = "@id"; pId.Value = id; cmd.Parameters.Add(pId);
                var pReason = cmd.CreateParameter(); pReason.ParameterName = "@reason"; pReason.Value = (object?)reason ?? DBNull.Value; cmd.Parameters.Add(pReason);
                var pStamp = cmd.CreateParameter(); pStamp.ParameterName = "@stamp"; pStamp.Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"); cmd.Parameters.Add(pStamp);
                await cmd.ExecuteNonQueryAsync();
            }


            // 2) Atzīmējam visas rindas kā neaktīvas
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE batches_products SET IsActive = 0 WHERE Batch_Id = @id;";
                var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id; cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new { batchId = id, deleted = true });
        }

        [HttpDelete("{batchId}/line/{versionId}")]
        public async Task<IActionResult> DeleteBatchLine(int batchId, int versionId)
        {
            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE batches_products
   SET IsActive = 0
 WHERE Batch_Id = @bid AND Version_Id = @vid;";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@bid"; p1.Value = batchId; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@vid"; p2.Value = versionId; cmd.Parameters.Add(p2);
                var affected = await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return Ok(new { batchId, versionId, affected });
            }
        }


        // POST: /api/batches/draft/update
        [HttpPost("draft/update")]
        public async Task<IActionResult> UpdateDraft([FromBody] BatchCartModel dto)
        {
            if (dto is null) return BadRequest("Tukšs pieprasījums.");
            if (!(dto.BatchId.HasValue && dto.BatchId > 0)) return BadRequest("BatchId ir obligāts.");

            var code = (dto.Title ?? "").Trim();

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            // 1) Pārbaude: nosaukumu pēc pirmās saglabāšanas mainīt nedrīkst
            await using (var chkName = conn.CreateCommand())
            {
                chkName.Transaction = tx;
                chkName.CommandText = @"SELECT Batches_Code FROM batches WHERE ID = @id;";
                var p = chkName.CreateParameter(); p.ParameterName = "@id"; p.Value = dto.BatchId!.Value; chkName.Parameters.Add(p);
                var current = (await chkName.ExecuteScalarAsync())?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(code) &&
                    !string.Equals(current, code, StringComparison.Ordinal))
                {
                    await tx.RollbackAsync();
                    return Conflict("Nosaukumu pēc pirmās saglabāšanas mainīt nevar.");
                }

            }

            // 2) Header UPDATE (komentārs, statuss paliek melnraksts)
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
UPDATE batches
   SET Comments = @comment,
       Batches_Statuss = 4,
       IsActive = 1
 WHERE ID = @id;";
                var p1 = cmd.CreateParameter(); p1.ParameterName = "@comment"; p1.Value = (object?)dto.Comment ?? DBNull.Value; cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter(); p2.ParameterName = "@id"; p2.Value = dto.BatchId!.Value; cmd.Parameters.Add(p2);
                await cmd.ExecuteNonQueryAsync();
            }

            // 3) Rindas (UPSERT pēc (Batch_Id, Version_Id))
if (dto.Items is not null && dto.Items.Count > 0)

{
    // 0️⃣ Deaktivējam VISAS rindas šim batch
await using (var clear = conn.CreateCommand())
{
    clear.Transaction = tx;
    clear.CommandText = @"
UPDATE batches_products
SET IsActive = 0
WHERE Batch_Id = @bid;";
    var p = clear.CreateParameter();
    p.ParameterName = "@bid";
    p.Value = dto.BatchId!.Value;
    clear.Parameters.Add(p);

    await clear.ExecuteNonQueryAsync();
}

    
    foreach (var it in dto.Items)
    {
        // ✅ Backend aizsardzība: nedrīkst mainīt produktu (VersionId)
        if (it.ItemId > 0)
        {
            await using var chk = conn.CreateCommand();
            chk.Transaction = tx;
            chk.CommandText = @"
SELECT Version_Id 
FROM batches_products 
WHERE ID = @id AND IsActive = 1;";
            var pid = chk.CreateParameter();
            pid.ParameterName = "@id";
            pid.Value = it.ItemId;
            chk.Parameters.Add(pid);

            var obj = await chk.ExecuteScalarAsync();
            var oldVid = (obj == null || obj == DBNull.Value) ? 0 : Convert.ToInt32(obj);

            if (oldVid != it.VersionId)
                return BadRequest("Produkta maiņa nav atļauta rediģēšanas režīmā.");
        }

        await using var row = conn.CreateCommand();
        row.Transaction = tx;
   row.CommandText = @"
INSERT INTO batches_products 
    (Batch_Id, Version_Id, Planned_Qty, Done_Qty, Priority, BatchProduct_Comments, IsActive)
VALUES 
    (@bid, @vid, @qty, 0, 0, @comment, 1)
ON DUPLICATE KEY UPDATE
    Planned_Qty           = VALUES(Planned_Qty),
    BatchProduct_Comments = VALUES(BatchProduct_Comments),
    IsActive              = 1;";


        var pb = row.CreateParameter();
        pb.ParameterName = "@bid";
        pb.Value = dto.BatchId!.Value;
        row.Parameters.Add(pb);

        var pv = row.CreateParameter();
        pv.ParameterName = "@vid";
        pv.Value = it.VersionId;
        row.Parameters.Add(pv);

        var pq = row.CreateParameter();
        pq.ParameterName = "@qty";
        pq.Value = it.Qty;
        row.Parameters.Add(pq);

        var pc = row.CreateParameter();
        pc.ParameterName = "@comment";
        pc.Value = (object?)it.Comment ?? DBNull.Value;
        row.Parameters.Add(pc);

        await row.ExecuteNonQueryAsync();
    }
}

            await tx.CommitAsync();
            return Ok(new { batchId = dto.BatchId!.Value });
        }

[HttpGet("by-batchproduct")]
public async Task<IActionResult> GetByBatchProduct([FromQuery] int batchProductId)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    bp.ID           AS BatchProductId,
    bp.Version_Id   AS VersionId,
    p.Product_Code  AS ProductCode,
    p.Product_Name  AS ProductName,
    c.Category_Name AS CategoryName
FROM batches_products bp
JOIN versions v   ON v.ID = bp.Version_Id
JOIN products p   ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID
WHERE bp.ID = @id
  AND bp.IsActive = 1;";

    var pId = cmd.CreateParameter();
    pId.ParameterName = "@id";
    pId.Value = batchProductId;
    cmd.Parameters.Add(pId);

    await using var r = await cmd.ExecuteReaderAsync();

    if (!await r.ReadAsync())
        return NotFound();

    return Ok(new
    {
        BatchProductId = r.GetInt32(0),
        VersionId      = r.GetInt32(1),
        ProductCode    = r.GetString(2),
        ProductName    = r.GetString(3),
        CategoryName   = r.GetString(4)
    });
}

// GET: /api/batches/by-id?batchProductId=513
[HttpGet("by-id")]
public async Task<IActionResult> GetByBatchProductId([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    bp.ID           AS BatchProductId,
    bp.Batch_Id     AS BatchId,
    bp.Version_Id   AS VersionId,
    p.Product_Code  AS ProductCode,
    c.Category_Name AS CategoryName
FROM batches_products bp
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID
WHERE bp.ID = @id
  AND bp.IsActive = 1
LIMIT 1;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@id";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    await using var r = await cmd.ExecuteReaderAsync();
    if (!await r.ReadAsync())
        return NotFound();

    return Ok(new
    {
        BatchProductId = r.GetInt32(0),
        BatchId        = r.GetInt32(1),
        VersionId      = r.GetInt32(2),
        ProductCode    = r.GetString(3),
        CategoryName   = r.GetString(4)
    });
}


// GET: /api/batches/list?batch_type=1
[HttpGet("list")]
public async Task<IActionResult> GetProductionBatches(
    [FromQuery] int batch_type = 1,
    [FromQuery] List<int>? versionIds = null)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    b.ID                               AS BatchId,
    b.Batches_Code                     AS BatchCode,
    bp.ID                              AS BatchProductId,
    bp.Version_Id                      AS VersionId,
    p.Product_Name                     AS ProductName,
    p.Product_Code                     AS ProductCode,
    c.Category_Name                    AS CategoryName,
    v.Version_Name                     AS VersionName,
    bp.is_priority                     AS IsPriority,

   -- Planned: tikai 1/5, nav 2/3
SUM(
    CASE
        WHEN tsum.DetPlannedCnt > 0
         AND tsum.DetStartedCnt = 0
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS Planned,

-- Detailed IN PROGRESS:
-- ir vismaz viens Detailed ar 2 VAI 3
-- UN ir vēl kāds Detailed ar 1/2/5 (tātad nav 100% pabeigts)
SUM(
    CASE
        WHEN tsum.DetStartedCnt > 0
         AND tsum.DetNotFinishedCnt > 0
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS DetailedInProgress,

    -- Detailed FINISH:
    -- visi Detailed = 3, nav vairs 1/2/5
    -- UN Assembly vēl NAV sācies (nav statusu 2 vai 3)
    SUM(
    CASE
        WHEN tsum.DetStartedCnt > 0
         AND tsum.DetNotFinishedCnt = 0
         AND tsum.AsmStartedCnt = 0
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS DetailedFinish,

    -- Assembly IN PROGRESS:
    -- Ir vismaz viens Assembly ar 2 VAI 3
    -- UN ir vēl kāds Assembly ar 1/2/5 (nav 100% pabeigts)
    SUM(
    CASE
        WHEN tsum.AsmStartedCnt > 0
         AND tsum.AsmNotFinishedCnt > 0
        THEN bp.Planned_Qty
        ELSE 0
    END
) AS Assembly,

    -- Assembly FINISH:
    -- ir vismaz viens Assembly ar 3
    -- UN vairs nav neviena Assembly ar 1/2/5
-- Assembly FINISH:
SUM(COALESCE(sm.AssemblyDone, 0)) AS Done,

SUM(COALESCE(sm.AssemblyDone, 0)) 
- SUM(COALESCE(tsum.FinishingStartedQty, 0)) 
AS AssemblyFinish,

SUM(COALESCE(tsum.FinishingStartedQty, 0)) AS FinishingInProgress

FROM batches_products bp
JOIN batches  b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p   ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID AND c.IsActive = 1

LEFT JOIN (
    SELECT
        BatchProduct_ID,
        SUM(CASE WHEN Move_Type = 'ASSEMBLY' THEN Stock_Qty ELSE 0 END)
        -
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END)
        AS AssemblyDone
    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) sm ON sm.BatchProduct_ID = bp.ID

LEFT JOIN (
    SELECT
        BatchProduct_ID,
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS Sold
    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) ss ON ss.BatchProduct_ID = bp.ID
LEFT JOIN (
    SELECT
        t.BatchProduct_ID,

        -- Detailed
        SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (1,5) THEN 1 ELSE 0 END) AS DetPlannedCnt,
        SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (2,3) THEN 1 ELSE 0 END) AS DetStartedCnt,
        SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) AS DetNotFinishedCnt,

        -- Assembly
        SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (2,3) THEN 1 ELSE 0 END) AS AsmStartedCnt,
        SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (1,2,5) THEN 1 ELSE 0 END) AS AsmNotFinishedCnt,

        -- Finishing
        0 AS FinishingStartedQty

    FROM tasks t
    JOIN toppartsteps ts 
    ON ts.ID = t.TopPartStep_ID
    AND ts.IsActive = 1
    AND t.IsActive = 1
    WHERE t.IsActive = 1
    GROUP BY t.BatchProduct_ID
) tsum ON tsum.BatchProduct_ID = bp.ID

WHERE
    bp.IsActive = 1
    AND b.IsActive = 1
    AND b.Batches_Statuss = 1
    AND (@useFilter = 0 OR bp.Version_Id IN ({versionFilter}))
    -- ja tev IR kolonna, kas atbilst batch_type (piem. b.Batches_Type_Id),
    -- te vari pielikt filtru, piem.:
    -- AND b.Batches_Type_Id = @batchType
GROUP BY
    bp.Version_Id,
    p.Product_Name,
    p.Product_Code
ORDER BY
    p.Product_Name;";

var useFilter = versionIds != null && versionIds.Any();

if (useFilter)
{
    var paramNames = new List<string>();

    for (int i = 0; i < versionIds!.Count; i++)
    {
        var paramName = $"@v{i}";
        paramNames.Add(paramName);

        var p = cmd.CreateParameter();
        p.ParameterName = paramName;
        p.Value = versionIds[i];
        cmd.Parameters.Add(p);
    }

    var inClause = string.Join(",", paramNames);
    cmd.CommandText = cmd.CommandText.Replace("{versionFilter}", inClause);
}
else
{
    cmd.CommandText = cmd.CommandText.Replace("{versionFilter}", "NULL");
}

var pFilter = cmd.CreateParameter();
pFilter.ParameterName = "@useFilter";
pFilter.Value = useFilter ? 1 : 0;
cmd.Parameters.Add(pFilter);
    // ja izmanto filtru pēc tipa, atkomentē šo:
    // cmd.Parameters.Add(new MySqlParameter("@batchType", batch_type));

    var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
list.Add(new
{
    BatchId             = r.GetInt32(0),
    BatchCode           = r.GetString(1),
    BatchProductId      = r.GetInt32(2),
    VersionId           = r.GetInt32(3),
    ProductName         = r.GetString(4),
    ProductCode         = r.GetString(5),
    CategoryName        = r.GetString(6),
    VersionName         = r.GetString(7),
    IsPriority          = r.GetBoolean(8),
    Planned             = r.GetInt32(9),
    DetailedInProgress  = r.GetInt32(10),
    DetailedFinish      = r.GetInt32(11),
    Assembly            = r.GetInt32(12),
    Done                = r.GetInt32(13),
    AssemblyFinish      = r.GetInt32(14),
    FinishingInProgress = r.GetInt32(15)
});

    }

    return Ok(list);
}

// POST: /api/batches/planned
// body: { "batchId": 57, "code": "RP-2026-001" }
[HttpPost("planned")]
public async Task<IActionResult> SetPlanned([FromBody] SetPlannedDto? dto)
{
    if (dto is null)
        return BadRequest("Body is required.");

    if (dto.BatchId <= 0)
        return BadRequest("BatchId is required.");

    if (string.IsNullOrWhiteSpace(dto.Code))
        return BadRequest("Code is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1️⃣ Pārslēdzam header uz Planned (1) + iestatām KODU
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
UPDATE batches
   SET Batches_Statuss = 1,
       Batches_Code    = @code
 WHERE ID = @bid
   AND IsActive = 1;";

        var pBid = cmd.CreateParameter();
        pBid.ParameterName = "@bid";
        pBid.Value = dto.BatchId;
        cmd.Parameters.Add(pBid);

        var pCode = cmd.CreateParameter();
        pCode.ParameterName = "@code";
        pCode.Value = dto.Code.Trim();
        cmd.Parameters.Add(pCode);

        var affected = await cmd.ExecuteNonQueryAsync();
        if (affected == 0)
            return NotFound("Batch not found or inactive.");
    }

    // 2️⃣ Ģenerējam tasks visiem soļiem šai partijai (statuss = 5)
    int tasksCreated;

    await using (var tcmd = conn.CreateCommand())
    {
        tcmd.CommandText = @"
INSERT INTO tasks
    (BatchProduct_ID, TopPartStep_ID, Tasks_Status,
     Tasks_Priority, Qty_Done, Qty_Scrap,
     Assigned_To, Claimed_By, IsActive)
SELECT
    bp.ID,
    ts.ID,
    5,
    0,
    0,
    0,
    NULL,
    NULL,
    1
FROM batches_products bp
JOIN producttopparts ptp
     ON ptp.Version_ID = bp.Version_Id
    AND ptp.IsActive = 1
JOIN toppartsteps ts
     ON ts.ProductToPart_ID = ptp.ID
    AND ts.IsActive = 1
LEFT JOIN tasks t
     ON t.BatchProduct_ID = bp.ID
    AND t.TopPartStep_ID = ts.ID
    AND t.IsActive = 1
WHERE bp.Batch_Id = @bid
  AND bp.IsActive = 1
  AND ptp.IsActive = 1
  AND ts.IsActive = 1
  AND t.ID IS NULL;";

        var pbid = tcmd.CreateParameter();
        pbid.ParameterName = "@bid";
        pbid.Value = dto.BatchId;
        tcmd.Parameters.Add(pbid);

        tasksCreated = await tcmd.ExecuteNonQueryAsync();
    }

    // 3️⃣ STOCK_MOVEMENTS: -PLANNED (rezervējam apjomu)
await using (var smCmd = conn.CreateCommand())
{
    smCmd.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, IsActive)
SELECT
    bp.Version_Id,
    bp.ID,
    'PLANNED',
    -bp.Planned_Qty,
    UTC_TIMESTAMP(),
    1
FROM batches_products bp
WHERE bp.Batch_Id = @bid
  AND bp.IsActive = 1;";

    var pBid2 = smCmd.CreateParameter();
    pBid2.ParameterName = "@bid";
    pBid2.Value = dto.BatchId;
    smCmd.Parameters.Add(pBid2);

    await smCmd.ExecuteNonQueryAsync();
}

    return Ok(new
    {
        batchId = dto.BatchId,
        status = 1,
        tasksCreated
    });

}

[HttpPost("draft/delete")]
public async Task<IActionResult> DeleteDraft([FromBody] DeleteDraftDto dto)
{
    if (dto.BatchId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1️⃣ soft delete batch items
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
UPDATE batches_products
   SET IsActive = 0
 WHERE Batch_Id = @bid AND IsActive = 1;
";
        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = dto.BatchId;
        cmd.Parameters.Add(p);

        await cmd.ExecuteNonQueryAsync();
    }

    // 2️⃣ batch status → 5
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
UPDATE batches
   SET Batches_Statuss = 5
 WHERE ID = @bid AND IsActive = 1;
";
        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = dto.BatchId;
        cmd.Parameters.Add(p);

        await cmd.ExecuteNonQueryAsync();
    }

    return Ok();
}

public sealed class DeleteDraftDto
{
    public int BatchId { get; set; }
}


[HttpGet("by-version")]
public async Task<IActionResult> GetByVersion([FromQuery] int versionId, [FromQuery] int batch_type = 1)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
  b.ID           AS BatchId,
  b.Batches_Code AS BatchCode,
  bp.ID          AS BatchProductId,
  bp.Version_Id  AS VersionId,

  bp.Planned_Qty AS Planned,

  -- ✅ Stage balances (nekad nerādām mīnusu)
  GREATEST(COALESCE(s.Detailed,  0), 0) AS Detailed,
  GREATEST(COALESCE(s.Assembly,  0), 0) AS Assembly,
  GREATEST(COALESCE(s.Finishing, 0), 0) AS Finishing,
  GREATEST(COALESCE(s.Stock,     0), 0) AS Done,

  b.Comments AS Comment,
  (
      SELECT MIN(t.Started_At)
      FROM tasks t
      WHERE t.BatchProduct_ID = bp.ID
        AND t.IsActive = 1
        AND t.Started_At IS NOT NULL
  ) AS StartedAt,

  b.Batches_Statuss AS BatchStatus

FROM batches b
JOIN batches_products bp 
      ON b.ID = bp.Batch_Id 
     AND bp.IsActive = 1

LEFT JOIN (
    SELECT 
      sm.BatchProduct_ID AS BatchProductId,

      SUM(CASE WHEN sm.Move_Type = 'DETAILED'  THEN sm.Stock_Qty ELSE 0 END) AS Detailed,
      SUM(CASE WHEN sm.Move_Type = 'ASSEMBLY'  THEN sm.Stock_Qty ELSE 0 END) AS Assembly,
      SUM(CASE WHEN sm.Move_Type = 'FINISHING' THEN sm.Stock_Qty ELSE 0 END) AS Finishing,
      SUM(CASE WHEN sm.Move_Type = 'STOCK'     THEN sm.Stock_Qty ELSE 0 END) AS Stock

    FROM stock_movements sm
    WHERE sm.IsActive = 1
    GROUP BY sm.BatchProduct_ID
) s ON s.BatchProductId = bp.ID

WHERE bp.Version_Id      = @vid
  AND b.Batches_Statuss  = @type
  AND b.IsActive         = 1
ORDER BY b.ID DESC;
";

    var pVid = cmd.CreateParameter();
pVid.ParameterName = "@vid";
pVid.Value = versionId;
cmd.Parameters.Add(pVid);

var pTyp = cmd.CreateParameter();
pTyp.ParameterName = "@type";
pTyp.Value = batch_type;
cmd.Parameters.Add(pTyp);


    var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        list.Add(new
        {
            BatchId        = r.GetInt32(0),
            BatchCode      = r.GetString(1),
            BatchProductId = r.GetInt32(2),
            Version_Id     = r.GetInt32(3),

            Planned   = r.GetInt32(4),
            Detailed  = r.GetInt32(5),
            Assembly  = r.GetInt32(6),
            Finishing = r.GetInt32(7),
            Done      = r.GetInt32(8),

            Comment     = r.IsDBNull(9)  ? null : r.GetString(9),
            StartedAt   = r.IsDBNull(10) ? (DateTime?)null : r.GetDateTime(10),
            BatchStatus = r.GetInt32(11)
        });
    }

    return Ok(list);
}


[HttpPost("update-batchproduct")]
public async Task<IActionResult> UpdateBatchProduct([FromBody] UpdateBatchProductDto dto)
{
    if (dto.BatchProductId <= 0)
        return BadRequest("BatchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1) Nolasa statusu un StartedAt šai rindai
    int status;
    DateTime? startedAt;

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT 
    b.Batches_Statuss,
    (
        SELECT MIN(t.Started_At)
        FROM tasks t
        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1
    ) AS StartedAt
FROM batches_products bp
JOIN batches b ON b.ID = bp.Batch_Id
WHERE bp.ID = @id
  AND bp.IsActive = 1;";

        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = dto.BatchProductId;
        cmd.Parameters.Add(p);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync())
            return NotFound("BatchProduct not found.");

        status    = r.GetInt32(0);
        startedAt = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1);
    }

    // 2) Loģika: qty drīkst mainīt tikai, ja:
    //    - statuss = 5 (VISIS)  VAI
    //    - nav StartedAt
    var canEditQty = (status == 5) || (startedAt == null);

    // 3) Atjaunojam
    await using (var upd = conn.CreateCommand())
    {
        upd.CommandText = @"
UPDATE batches_products
SET 
    Planned_Qty = CASE WHEN @canEdit = 1 THEN @qty ELSE Planned_Qty END,
    BatchProduct_Comments = @comment
WHERE ID = @id
  AND IsActive = 1;";

        var pid = upd.CreateParameter();
        pid.ParameterName = "@id";
        pid.Value = dto.BatchProductId;
        upd.Parameters.Add(pid);

        var pq = upd.CreateParameter();
        pq.ParameterName = "@qty";
        pq.Value = dto.PlannedQty;
        upd.Parameters.Add(pq);

        var pc = upd.CreateParameter();
        pc.ParameterName = "@comment";
        pc.Value = (object?)dto.Comment ?? DBNull.Value;
        upd.Parameters.Add(pc);

        var pce = upd.CreateParameter();
        pce.ParameterName = "@canEdit";
        pce.Value = canEditQty ? 1 : 0;
        upd.Parameters.Add(pce);

        await upd.ExecuteNonQueryAsync();
    }

    // ŠEIT GALVENĀ IZMAIŅA:
    // vairs nemet 409 – atgriežam info, kas notika
    return Ok(new 
    { 
        Ok = true, 
        QuantityChanged = canEditQty 
    });
}

[HttpGet("draft/last")]
public async Task<IActionResult> GetLastDraft()
{
    var cs = _db.Database.GetConnectionString();
    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync();

    // 1) atrodam vienīgo aktīvo melnrakstu
    int? batchId = null;
    string? comment = null;

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT ID, Comments
FROM batches
WHERE Batches_Statuss = 4
  AND IsActive = 1
LIMIT 1;
";
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            batchId = reader.GetInt32(0);
            comment = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
    }

    if (!batchId.HasValue)
        return NoContent(); // melnraksta nav

    // 2) atrodam melnraksta preces
    var items = new List<object>();

    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
SELECT Version_Id, Planned_Qty, BatchProduct_Comments
FROM batches_products
WHERE Batch_Id = @bid
  AND IsActive = 1;
";
        var p = cmd.CreateParameter();
        p.ParameterName = "@bid";
        p.Value = batchId.Value;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                VersionId = reader.GetInt32(0),
                Qty       = reader.GetInt32(1),
                Comment   = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }
    }

    // 3) atgriežam rezultātu
    return Ok(new
    {
        BatchId = batchId.Value,
        Comment = comment,
        Items   = items
    });
}



 // POST: /api/batches/update-line
[HttpPost("update-line")]
public async Task<IActionResult> UpdateLine([FromBody] UpdateBatchProductDto? dto)
{
    if (dto is null)
        return BadRequest("Body is required.");
    if (dto.BatchProductId <= 0)
        return BadRequest("BatchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    // 1) Nolasām pašreizējo Planned_Qty
    int currentQty;
    await using (var cmdSel = conn.CreateCommand())
    {
        cmdSel.Transaction = tx;
        cmdSel.CommandText = @"
SELECT Planned_Qty
FROM batches_products
WHERE ID = @id AND IsActive = 1;";
        var p = cmdSel.CreateParameter();
        p.ParameterName = "@id";
        p.Value = dto.BatchProductId;
        cmdSel.Parameters.Add(p);

        var obj = await cmdSel.ExecuteScalarAsync();
        if (obj is null || obj == DBNull.Value)
        {
            await tx.RollbackAsync();
            return NotFound("Batch product not found.");
        }

        currentQty = Convert.ToInt32(obj);
    }

    // 2) Ja mēģina mainīt daudzumu – pārbaudām, vai darbs nav sācies
    if (dto.PlannedQty != currentQty)
    {
        await using var cmdChk = conn.CreateCommand();
        cmdChk.Transaction = tx;
        cmdChk.CommandText = @"
SELECT COUNT(*)
FROM tasks t
WHERE t.BatchProduct_ID = @bpId
  AND t.IsActive = 1
  AND t.Tasks_Status IN (1,2,3);";
        var p2 = cmdChk.CreateParameter();
        p2.ParameterName = "@bpId";
        p2.Value = dto.BatchProductId;
        cmdChk.Parameters.Add(p2);

        var cnt = Convert.ToInt32(await cmdChk.ExecuteScalarAsync());
        if (cnt > 0)
        {
            await tx.RollbackAsync();
            return BadRequest("Daudzumu nevar mainīt, jo darbs jau ir uzsākts.");
        }
    }

    // 3) Atjaunojam Planned_Qty + komentāru
    await using (var cmdUpd = conn.CreateCommand())
    {
        cmdUpd.Transaction = tx;
        cmdUpd.CommandText = @"
UPDATE batches_products
   SET Planned_Qty = @qty,
       BatchProduct_Comments = @comment
 WHERE ID = @id;";

        var pId = cmdUpd.CreateParameter();
        pId.ParameterName = "@id";
        pId.Value = dto.BatchProductId;
        cmdUpd.Parameters.Add(pId);

        var pQty = cmdUpd.CreateParameter();
        pQty.ParameterName = "@qty";
        pQty.Value = dto.PlannedQty;
        cmdUpd.Parameters.Add(pQty);

        var pCom = cmdUpd.CreateParameter();
        pCom.ParameterName = "@comment";
        pCom.Value = (object?)dto.Comment ?? DBNull.Value;
        cmdUpd.Parameters.Add(pCom);

        await cmdUpd.ExecuteNonQueryAsync();
    }

    await tx.CommitAsync();

    return Ok(new
    {
        batchProductId = dto.BatchProductId,
        plannedQty = dto.PlannedQty,
        comment = dto.Comment
    });
}

// POST: /api/batches/update-comment
[HttpPost("update-comment")]
public async Task<IActionResult> UpdateBatchComment([FromBody] UpdateBatchCommentDto dto)
{
    if (dto.BatchId <= 0)
        return BadRequest("BatchId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
UPDATE batches
SET Comments = @comment
WHERE ID = @id
  AND IsActive = 1;";

    var pId = cmd.CreateParameter();
    pId.ParameterName = "@id";
    pId.Value = dto.BatchId;
    cmd.Parameters.Add(pId);

    var pCom = cmd.CreateParameter();
    pCom.ParameterName = "@comment";
    pCom.Value = (object?)dto.Comment ?? DBNull.Value;
    cmd.Parameters.Add(pCom);

    await cmd.ExecuteNonQueryAsync();

    return Ok(new { batchId = dto.BatchId });
}

[HttpGet("list-production")]
public async Task<IActionResult> GetProductionBatchesRows()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    b.ID           AS BatchId,
    b.Batches_Code AS BatchCode,
    bp.ID          AS BatchProductId,
    bp.Version_Id  AS VersionId,
    p.Product_Name AS ProductName,
    p.Product_Code AS ProductCode,
    c.Category_Name AS CategoryName,
    v.Version_Name  AS VersionName,
    bp.is_priority  AS IsPriority,
    bp.Planned_Qty  AS Planned, 
    bp.BatchProduct_Comments AS Comment
, COALESCE(tsum.DetailedInProgress, 0)  AS DetailedInProgress
, COALESCE(tsum.DetailedFinish, 0)      AS DetailedFinish
, COALESCE(tsum.AssemblyInProgress, 0)  AS AssemblyInProgress
, COALESCE(sm.AssemblyDone, 0)      AS AssemblyFinish
, COALESCE(sm.Sold, 0)              AS Sold
, COALESCE(tsum.FinishingInProgress, 0) AS FinishingInProgress
, COALESCE(tsum.FinStatus1, 0) AS FinStatus1
, COALESCE(tsum.FinStatus2, 0) AS FinStatus2
, COALESCE(tsum.FinStatus3, 0) AS FinStatus3
, tsum.DetailStart
, tsum.DetailFinish
, tsum.AssemblyStart
, tsum.AssemblyFinishDate
, COALESCE(tsum.DetailedStarted, 0) AS DetailedStarted
, COALESCE(pp.DetailsTotal, 0) AS DetailsTotal
FROM batches_products bp
JOIN batches  b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID
LEFT JOIN (
    SELECT 
        BatchProduct_ID,

SUM(CASE 
        WHEN Move_Type = 'ASSEMBLY' AND Stock_Qty > 0 
        THEN Stock_Qty 
        ELSE 0 
    END)
-
SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END)
AS AssemblyDone,

SUM(CASE WHEN Move_Type = 'FINISHING' THEN Stock_Qty ELSE 0 END) AS FinishingSent,

SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS Sold

    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) sm ON sm.BatchProduct_ID = bp.ID

LEFT JOIN (
    SELECT
        t.BatchProduct_ID,
        CASE
    WHEN SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (1,2,3) THEN 1 ELSE 0 END) > 0
     AND SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) > 0
    THEN MAX(bp2.Planned_Qty)
    ELSE 0
END AS DetailedInProgress,

CASE
    WHEN SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) = 0
    THEN MAX(bp2.Planned_Qty)
    ELSE 0
END AS DetailedFinish,

        CASE
            WHEN SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (2,3) THEN 1 ELSE 0 END) > 0
             AND SUM(CASE WHEN ts.Step_Type = 2 AND t.Tasks_Status IN (1,2,5) THEN 1 ELSE 0 END) > 0
            THEN MAX(bp2.Planned_Qty)
            ELSE 0
        END AS AssemblyInProgress,

SUM(CASE 
        WHEN ts.Step_Type = 3 AND t.Tasks_Status <> 5
        THEN t.Qty_Done 
        ELSE 0
    END) AS FinishingInProgress,
SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 1 THEN t.Qty_Done ELSE 0 END) AS FinStatus1,
SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 2 THEN t.Qty_Done ELSE 0 END) AS FinStatus2,
SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 3 THEN t.Qty_Done ELSE 0 END) AS FinStatus3,

MIN(CASE 
        WHEN ts.Step_Type = 1 
         AND t.Tasks_Status IN (2,3)
         AND t.Started_At IS NOT NULL
        THEN t.Started_At 
      END) AS DetailStart,

CASE
    WHEN SUM(CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status <> 3 THEN 1 ELSE 0 END) = 0
    THEN MAX(CASE 
        WHEN ts.Step_Type = 1 AND t.Tasks_Status = 3 
        THEN t.Finished_At 
    END)
    ELSE NULL
END AS DetailFinish,

MIN(CASE 
        WHEN ts.Step_Type = 2 AND t.Tasks_Status = 2
        THEN t.Started_At
      END) AS AssemblyStart,

MAX(CASE 
        WHEN ts.Step_Type = 2 AND t.Tasks_Status = 3
        THEN t.Finished_At
      END) AS AssemblyFinishDate,

COUNT(DISTINCT CASE 
      WHEN mdet.Stage = 1 AND t.Tasks_Status IN (1,2,3)
      THEN ptp.ID
  END) AS DetailedStarted
    FROM tasks t
    JOIN toppartsteps ts 
  ON ts.ID = t.TopPartStep_ID
 AND ts.IsActive = 1
    JOIN producttopparts ptp 
        ON ptp.ID = ts.ProductToPart_ID
        AND ptp.IsActive = 1
            LEFT JOIN stage_step_type_map mdet
            ON mdet.Stage = 1
            AND mdet.IsActive = 1
            AND mdet.Step_Type_ID = ts.Step_Type
JOIN batches_products bp2 ON bp2.ID = t.BatchProduct_ID
WHERE t.IsActive = 1
  AND ts.IsActive = 1
GROUP BY t.BatchProduct_ID
) tsum ON tsum.BatchProduct_ID = bp.ID
LEFT JOIN (
    SELECT
        ptp.Version_ID,
        COUNT(DISTINCT ptp.ID) AS DetailsTotal
    FROM producttopparts ptp
    JOIN toppartsteps ts
         ON ts.ProductToPart_ID = ptp.ID
        AND ts.IsActive = 1
    JOIN stage_step_type_map mdet
         ON mdet.Stage = 1
        AND mdet.IsActive = 1
        AND mdet.Step_Type_ID = ts.Step_Type
    WHERE ptp.IsActive = 1
    GROUP BY ptp.Version_ID
) pp ON pp.Version_ID = bp.Version_Id
WHERE bp.IsActive = 1
  AND b.IsActive = 1
  AND b.Batches_Statuss = 1
ORDER BY b.ID DESC;
";

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            BatchId        = r.GetInt32(0),
            BatchCode      = r.GetString(1),
            BatchProductId = r.GetInt32(2),
            VersionId      = r.GetInt32(3),
            ProductName    = r.GetString(4),
            ProductCode    = r.GetString(5),
            CategoryName   = r.GetString(6),
            VersionName    = r.GetString(7),
            IsPriority     = r.GetBoolean(8),
            Planned = r.GetInt32(9),
            Comment = r.IsDBNull(10) ? null : r.GetString(10),
            DetailedInProgress = r.GetInt32(11),
            DetailedFinish      = r.GetInt32(12),
            AssemblyInProgress  = r.GetInt32(13),
            AssemblyFinish      = r.GetInt32(14),
            Sold                = r.GetInt32(15),
            FinishingInProgress = r.GetInt32(16),
            FinStatus1 = r.IsDBNull(17) ? 0 : r.GetInt32(17),
            FinStatus2 = r.IsDBNull(18) ? 0 : r.GetInt32(18),
            FinStatus3 = r.IsDBNull(19) ? 0 : r.GetInt32(19),

            DetailStart  = r.IsDBNull(20) ? (DateTime?)null : r.GetFieldValue<DateTime>(20),
            DetailFinish = r.IsDBNull(21) ? (DateTime?)null : r.GetFieldValue<DateTime>(21),

            AssemblyStart  = r.IsDBNull(22) ? (DateTime?)null : r.GetFieldValue<DateTime>(22),
            AssemblyFinishDate = r.IsDBNull(23) ? (DateTime?)null : r.GetFieldValue<DateTime>(23),

            DetailedStarted = r.GetInt32(24),
            DetailsTotal = r.GetInt32(25)
        });
    }

    return Ok(list);
}

// GET: /api/batches/assembly-out?batchProductId=123
[HttpGet("assembly-out")]
public async Task<IActionResult> GetAssemblyOut([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COALESCE(SUM(Stock_Qty),0)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'OUT'
  AND IsActive = 1
  AND Stock_Qty > 0;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var result = await cmd.ExecuteScalarAsync();
    var qty = (result == null || result == DBNull.Value) ? 0 : Convert.ToInt32(result);

    return Ok(qty);
}

// GET: /api/batches/finishing-out
[HttpGet("finishing-out")]
public async Task<IActionResult> GetFinishingOut([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COALESCE(SUM(Stock_Qty),0)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'FINISHING'
  AND IsActive = 1
  AND Stock_Qty < 0;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var result = await cmd.ExecuteScalarAsync();
    var qty = result == null ? 0 : Math.Abs(Convert.ToInt32(result));

    return Ok(qty);
}

// GET: api/batches/assembly-out-total?batchProductId=123
[HttpGet("assembly-out-total")]
public async Task<IActionResult> GetAssemblyOutTotal([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    COALESCE(SUM(CASE WHEN Move_Type='ASSEMBLY' THEN Stock_Qty END),0)
  - COALESCE(SUM(CASE WHEN Move_Type='OUT' THEN Stock_Qty END),0)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND IsActive = 1;";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var result = await cmd.ExecuteScalarAsync();
    var qty = result == null ? 0 : Math.Abs(Convert.ToInt32(result));

    return Ok(qty);
}

    } // <-- beidzas public class BatchesController

    // === DTO (tie paši nosaukumi, ko izmanto Blazor) ===
    public sealed class BatchCartModel
    {
        public int? BatchId { get; set; }   // create = null, update = >0
        public string Title { get; set; } = "";
        public string? Comment { get; set; }
        public List<BatchCartItem> Items { get; set; } = new();
    }
public sealed class BatchCartItem
{
    public int ProductId { get; set; }
    public int VersionId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public int Qty { get; set; }

    public string? Comment { get; set; }   // ✅ JAUNS

    public int? ItemId { get; set; }
}

public sealed class UpdateBatchProductDto
{
    public int BatchProductId { get; set; }
    public int PlannedQty { get; set; }
    public string? Comment { get; set; }
}

public sealed class SetPlannedDto
{
    public int BatchId { get; set; }
    public string Code { get; set; } = "";
}

public sealed class UpdateBatchCommentDto
{
    public int BatchId { get; set; }
    public string? Comment { get; set; }
}


} // <-- beidzas namespace ManiApi.Controllers


c) // Preču API kontrolieris
// Šeit ir metodes, kas saistītas ar precēm, to versijām, detaļām un tehnoloģiju soļiem.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;
using MySqlConnector;

namespace ManiApi.Controllers

{ 
    public class CreateProductRequest
    {
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public int CategoryId { get; set; }

    // Versijas lauki (visi nav obligāti)
    public string? VersionName { get; set; }
    public string? VersionRasejums { get; set; }
    public string? VersionDate { get; set; }
    public string? VersionComment { get; set; }
    }
public class UpdateProductRequest
{
    public int ProductId { get; set; }

    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public int CategoryId { get; set; }

    public bool CreateNewVersion { get; set; }  // true → izveido jaunu versiju
    public int? VersionId { get; set; }         // vajadzīgs, ja labo esošo (CreateNewVersion=false)

    public string? VersionName { get; set; }
    public string? VersionRasejums { get; set; }
    public string? VersionDate { get; set; }    // "yyyy-MM-dd"
    public string? VersionComment { get; set; }
    public bool CopyTechnologySteps { get; set; } // true -> kopēt soļus jaunajai versijai

}

public class CreateStepRequest
{
    public int ProductToPartId { get; set; }   // ProductTopPart.Id
    public int StepOrder { get; set; }         // ja 0 → likšu max+10
    public string StepName { get; set; } = "";
    public int StepType { get; set; }          // StepTypes.Id
    public int WorkCentrId { get; set; }       // WorkCentrs.Id
    public int? EstimatedMinutes { get; set; }
    public int ParallelGroup { get; set; } = 0;
    public bool IsMandatory { get; set; }
    public bool IsFinal { get; set; }
    public string? Comments { get; set; }
}

public class UpdateStepRequest
{
    public int Id { get; set; }                // TopPartSteps.Id
    public int StepOrder { get; set; }
    public string StepName { get; set; } = "";
    public int StepType { get; set; }
    public int WorkCentrId { get; set; }
    public int? EstimatedMinutes { get; set; }
    public int ParallelGroup { get; set; } = 0;
    public bool IsMandatory { get; set; }
    public bool IsFinal { get; set; }
    public string? Comments { get; set; }
}

    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ProductsController(AppDbContext db) => _db = db;


[HttpGet("list")]
[ProducesResponseType(typeof(IEnumerable<ProductListItemDto>), StatusCodes.Status200OK)]
public async Task<ActionResult<IEnumerable<ProductListItemDto>>> GetList()

{
var rows = await (
    from p in _db.Products.AsNoTracking()
    join v in _db.ProductVersions on p.Id equals v.ProductId
    join c in _db.Categories on p.CategoryId equals c.Id
    join pc in _db.Categories on c.ParentId equals pc.Id into parentJoin
    from pc in parentJoin.DefaultIfEmpty()
    select new
    {
        p.Id,
        p.ProductCode,
        p.ProductName,
        p.IsActive,

        CategoryName = c.IsActive ? c.CategoryName : null,
        RootName = c.ParentId == null
            ? c.CategoryName
            : (pc != null && pc.IsActive ? pc.CategoryName : null),

        VersionName = v.VersionName,
        VersionDate = v.VersionDate,
        IsPriority = v.IsPriority,
        Version_Id = v.Id,
        VersionIsActive = v.IsActive
    }
)
.OrderBy(x => x.RootName)
.ThenBy(x => x.CategoryName)
.ThenBy(x => x.ProductName)
.ThenByDescending(x => x.VersionDate)
.ToListAsync();

var result = rows.Select(x => new ProductListItemDto
{
    Id = x.Id,
    ProductCode = x.ProductCode,
    ProductName = x.ProductName,
    CategoryName = x.CategoryName,
    RootName = x.RootName,
    VersionId = x.Version_Id,
    VersionName = x.VersionName,
    VersionDate = x.VersionDate,
    IsPriority = x.IsPriority,

    IsActive = x.IsActive,
    VersionIsActive = x.VersionIsActive,

    GroupType =
        string.Equals(x.RootName, "KAUSS", StringComparison.OrdinalIgnoreCase) ? 1 :
        string.Equals(x.RootName, "ADAPTERIS", StringComparison.OrdinalIgnoreCase) ? 2 : 0
});


    return Ok(result);
}

        
        [HttpGet("list-simple")]
public async Task<IActionResult> GetListSimple()
{
    var rows = await _db.Products.AsNoTracking()
        .Where(p => p.IsActive)
        .Select(p => new
        {
            p.Id,
            p.ProductCode,
            p.ProductName,
            CategoryName = _db.Categories
                .Where(c => c.Id == p.CategoryId && c.IsActive)
                .Select(c => c.CategoryName)
                .FirstOrDefault()
        })
        .ToListAsync();

    var result = rows.Select(x => new
    {
        x.Id,
        x.ProductCode,
        x.ProductName,
        x.CategoryName
    });

    return Ok(result);
}

        // JAUNĀ METODE: GET /api/products/content?id={id}
[HttpGet("content")]
public async Task<IActionResult> GetContent([FromQuery] int versionId)
{
    var version = await _db.ProductVersions
        .AsNoTracking()
        .Where(v => v.Id == versionId)
        .Select(v => new
        {
            v.Id,
            v.VersionName,
            v.VersionRasejums,
            v.VersionDate,
            v.VersionComment,
            v.ProductId
        })
        .FirstOrDefaultAsync();

    if (version is null)
        return NotFound();

    var product = await _db.Products
        .AsNoTracking()
        .Where(p => p.Id == version.ProductId)
        .Select(p => new
        {
            p.ProductName,
            p.ProductCode,
            p.CategoryId
        })
        .FirstOrDefaultAsync();

    if (product is null)
        return NotFound();

    var categoryName = await _db.Categories
        .AsNoTracking()
        .Where(c => c.Id == product.CategoryId && c.IsActive)
        .Select(c => c.CategoryName)
        .FirstOrDefaultAsync();

    return Ok(new
    {
        VersionId = version.Id,
        CategoryName = categoryName,
        ProductName = product.ProductName,
        ProductCode = product.ProductCode,
        VersionName = version.VersionName,
        VersionRasejums = version.VersionRasejums,
        VersionDate = version.VersionDate,
        VersionComment = version.VersionComment
    });
}

        [HttpGet("details")]
public async Task<IActionResult> GetDetails(
    [FromQuery] int versionId,
    [FromQuery] int stepType
)
{
    var rows = await _db.ProductTopParts
        .AsNoTracking()
        .Where(pt =>
            pt.VersionId == versionId &&
            pt.IsActive
        )
        .Select(pt => new
        {
            ProductToPartId = pt.Id, // ProductToPartId
            TopPartName = _db.TopParts
                .Where(tp => tp.Id == pt.TopPartId && tp.IsActive)
                .Select(tp => tp.TopPartName)
                .FirstOrDefault(),
            Quantity = pt.QtyPerProduct,

            Steps = _db.TopPartSteps
                .Where(s =>
                    s.ProductToPartId == pt.Id &&
                    s.IsActive &&
                    s.StepType == stepType
                )
                .OrderBy(s => s.StepOrder)
                .Select(s => new
                {
                    s.Id,
                    s.StepName,
                    s.StepOrder
                })
                .ToList()
        })
        .Where(x => x.Steps.Any()) // tikai detaļas ar DETAIL soļiem
        .ToListAsync();

    return Ok(rows);
}

[HttpGet("toppartsteps")]
public async Task<IActionResult> GetTopPartSteps(
    [FromQuery] int versionId,
    [FromQuery] int stepType
)
{
    var rows = await _db.TopPartSteps
        .AsNoTracking()
        .Where(ts =>
            ts.IsActive &&
            ts.StepType == stepType &&
            _db.ProductTopParts.Any(pt =>
                pt.Id == ts.ProductToPartId &&
                pt.VersionId == versionId &&
                pt.IsActive
            )
        )
        .Select(ts => new
        {
            Id = ts.Id,
            ProductToPartId = ts.ProductToPartId, // 🔑 SAIKNE AR ProductTopPart
            StepName = ts.StepName
        })
        .ToListAsync();

    return Ok(rows);
}


        [HttpGet("details-by-product")]
        public async Task<IActionResult> GetDetailsByProduct([FromQuery] int id)
        {
            // 1) aktīvs produkts
            var product = await _db.Products.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync();

            if (product is null)
                return NotFound();

            // 2) jaunākā aktīvā versija šim produktam
            var versionId = await _db.ProductVersions.AsNoTracking()
                .Where(v => v.ProductId == product.Id)
                .OrderByDescending(v => v.VersionDate)
                .Select(v => v.Id)
                .FirstOrDefaultAsync();

            if (versionId == 0)
                return NotFound();

            // 3) detaļas šai versijai (tikai aktīvās)
            var rows = await _db.ProductTopParts.AsNoTracking()
                .Where(pt => pt.VersionId == versionId && pt.IsActive)
                .Join(_db.TopParts.Where(tp => tp.IsActive),
                      pt => pt.TopPartId,
                      tp => tp.Id,
                      (pt, tp) => new
                      {
                          TopPartId = pt.TopPartId,
                          tp.TopPartName,
                          tp.TopPartCode,
                          tp.Stage,
                          Quantity = pt.QtyPerProduct,
                          ProductToPartId = pt.Id
                      })
                .ToListAsync();

            return Ok(rows);

        }

[HttpGet("details-by-version")]
public async Task<IActionResult> GetDetailsByVersion([FromQuery] int versionId)
{
    var rows = await _db.ProductTopParts.AsNoTracking()
        .Where(pt => pt.VersionId == versionId && pt.IsActive)
        .Join(_db.TopParts.Where(tp => tp.IsActive),
              pt => pt.TopPartId,
              tp => tp.Id,
              (pt, tp) => new
              {
                  TopPartId = pt.TopPartId,
                  tp.TopPartName,
                  tp.TopPartCode,
                  tp.Stage,
                  Quantity = pt.QtyPerProduct,
                  ProductToPartId = pt.Id
              })
        .ToListAsync();

    return Ok(rows);
}

[HttpGet("stage-step-type-map")]
public async Task<IActionResult> GetStageStepTypeMap()
{
    var rows = await _db.StageStepTypeMaps
        .Where(x => x.IsActive)
        .Select(x => new
        {
            x.Stage,
            x.Step_Type_ID,
            x.IsActive
        })
        .ToListAsync();

    return Ok(rows);
}

        [HttpGet("works-by-product")]
        public async Task<IActionResult> GetWorksByProduct([FromQuery] int id)
        {
            // atrodam aktīvu produktu
            var product = await _db.Products.AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync();

            if (product is null)
                return NotFound();

            // atrodam jaunāko aktīvo versiju
            var versionId = await _db.ProductVersions.AsNoTracking()
                .Where(v => v.ProductId == product.Id)
                .OrderByDescending(v => v.VersionDate)
                .Select(v => v.Id)
                .FirstOrDefaultAsync();

            if (versionId == 0)
                return NotFound();

            // savācam detaļas ar darbiem
            var result = await _db.ProductTopParts.AsNoTracking()
                .Where(pt => pt.VersionId == versionId && pt.IsActive)
                .Join(_db.TopParts.Where(tp => tp.IsActive),
                      pt => pt.TopPartId,
                      tp => tp.Id,
                      (pt, tp) => new { pt, tp })
                .Select(x => new
                {
                    x.tp.TopPartName,
                    x.tp.TopPartCode,
                    Steps = _db.TopPartSteps
                        .Where(s => s.ProductToPartId == x.pt.Id && s.IsActive)
                        .OrderBy(s => s.StepOrder)
                        .Join(_db.StepTypes.Where(st => st.IsActive),
                              s => s.StepType,
                              st => st.Id,
                              (s, st) => new { s, StepTypeName = st.StepTypeName })
                        .Join(_db.WorkCentrs.Where(wc => wc.IsActive),
                              temp => temp.s.WorkCentrId,
                              wc => wc.Id,
                              (temp, wc) => new
                              {
                                  temp.s.StepOrder,
                                  temp.s.StepName,
                                  StepType = temp.StepTypeName,
                                  WorkCenter = wc.WorkCentr_Name,
                                  temp.s.IsFinal,
                                  temp.s.IsMandatory,
                                  temp.s.Comments
                              })
                        .ToList()
                })
                .ToListAsync();

            return Ok(result);
        }

[HttpGet("works-by-version")]
public async Task<IActionResult> GetWorksByVersion([FromQuery] int versionId)
{
    var result = await _db.ProductTopParts.AsNoTracking()
        .Where(pt => pt.VersionId == versionId && pt.IsActive)
        .Join(_db.TopParts.Where(tp => tp.IsActive),
              pt => pt.TopPartId,
              tp => tp.Id,
              (pt, tp) => new { pt, tp })
        .Select(x => new
        {
            x.tp.TopPartName,
            x.tp.TopPartCode,
            Steps = _db.TopPartSteps
                .Where(s => s.ProductToPartId == x.pt.Id && s.IsActive)
                .OrderBy(s => s.StepOrder)
                .Join(_db.StepTypes.Where(st => st.IsActive),
                    s => s.StepType,
                    st => st.Id,
                    (s, st) => new { s, StepTypeName = st.StepTypeName })
                .Join(_db.WorkCentrs.Where(wc => wc.IsActive),
                    temp => temp.s.WorkCentrId,
                    wc => wc.Id,
                    (temp, wc) => new
                    {
                        temp.s.StepOrder,
                        temp.s.StepName,
                        StepType = temp.StepTypeName,
                        WorkCenter = wc.WorkCentr_Name,
                        temp.s.IsFinal,
                        temp.s.IsMandatory,
                        temp.s.Comments
                    })
                .ToList()
                
        })
        
        .ToListAsync();

    return Ok(result);
}
        // JAUNA METODE: POST /api/products/create        

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateProductRequest dto)
        {
            Console.WriteLine($"[CREATE] Name={dto.ProductName}, Code={dto.ProductCode}, Cat={dto.CategoryId}, " +
                              $"VerName={dto.VersionName}, VerRasejums={dto.VersionRasejums}, VerDate={dto.VersionDate}, VerComment={dto.VersionComment}");
            try
            {
                if (string.IsNullOrWhiteSpace(dto.ProductName) || string.IsNullOrWhiteSpace(dto.ProductCode))
                    return BadRequest("Nosaukums un kods ir obligāti.");

                var product = new Product
                {
                    ProductName = dto.ProductName,
                    ProductCode = dto.ProductCode,
                    CategoryId = dto.CategoryId,
                    IsActive = true
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                int? versionId = null;

                // Izveidojam versiju, ja ir vismaz viens versijas lauks
                if (!string.IsNullOrWhiteSpace(dto.VersionName)
                    || !string.IsNullOrWhiteSpace(dto.VersionRasejums)
                    || !string.IsNullOrWhiteSpace(dto.VersionDate)
                    || !string.IsNullOrWhiteSpace(dto.VersionComment))
                {
                    var parsedDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                    if (!string.IsNullOrWhiteSpace(dto.VersionDate)
                        && DateOnly.TryParse(dto.VersionDate, out var d))
                    {
                        parsedDate = d;
                    }

                    var ver = new ProductVersion
                    {
                        ProductId = product.Id,
                        VersionName = dto.VersionName ?? "",
                        VersionRasejums = dto.VersionRasejums ?? "",
                        VersionDate = parsedDate,
                        VersionComment = dto.VersionComment ?? "",
                        IsActive = true
                    };

                    _db.ProductVersions.Add(ver);
                    await _db.SaveChangesAsync();
                    versionId = ver.Id;
                }

                return Ok(new { product.Id, VersionId = versionId });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[API CREATE ERROR] " + ex.ToString());
                return StatusCode(500, "CREATE failed: " + ex.Message);
            }
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateProductRequest dto)
        {
            Console.WriteLine($"[API UPDATE] ProductId={dto.ProductId}, CreateNewVersion={dto.CreateNewVersion}, " +
                              $"VersionId={dto.VersionId}, VersionName={dto.VersionName}, " +
                              $"VersionDate={dto.VersionDate}, VersionRasejums={dto.VersionRasejums}, " +
                              $"VersionComment={dto.VersionComment}, CategoryId={dto.CategoryId}");
            try
            {
                if (dto.ProductId <= 0) return BadRequest("ProductId is required.");

                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);
                if (product is null) return NotFound("Product not found.");

                // ——— Product pamatlauki ———
                if (!string.IsNullOrWhiteSpace(dto.ProductName)) product.ProductName = dto.ProductName;
                if (!string.IsNullOrWhiteSpace(dto.ProductCode)) product.ProductCode = dto.ProductCode;
                if (dto.CategoryId > 0) product.CategoryId = dto.CategoryId;

                await _db.SaveChangesAsync();

                // ——— Versiju apstrāde ———
                if (dto.CreateNewVersion)
                {
                    // deaktivējam iepriekšējo aktīvo (pēc VersionId vai atrodam jaunāko)
                    ProductVersion? prev;
                    if (dto.VersionId.HasValue)
                    {
                        prev = await _db.ProductVersions
                            .FirstOrDefaultAsync(v => v.Id == dto.VersionId.Value && v.ProductId == product.Id && v.IsActive);
                    }
                    else
                    {
                        prev = await _db.ProductVersions
                            .Where(v => v.ProductId == product.Id && v.IsActive)
                            .OrderByDescending(v => v.VersionDate)
                            .FirstOrDefaultAsync();
                    }
                    if (prev is not null) prev.IsActive = false;

                    // jaunas versijas datums
                    var parsedDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
                    if (!string.IsNullOrWhiteSpace(dto.VersionDate))
                    {
                        if (!(DateOnly.TryParse(dto.VersionDate, out parsedDate) ||
                              DateOnly.TryParseExact(dto.VersionDate, "yyyy-MM-dd", null,
                                  System.Globalization.DateTimeStyles.None, out parsedDate)))
                        {
                            return BadRequest("Invalid VersionDate.");
                        }
                    }

                    var newVer = new ProductVersion
                    {
                        ProductId = product.Id,
                        VersionName = dto.VersionName ?? "",
                        VersionRasejums = dto.VersionRasejums ?? "",
                        VersionDate = parsedDate,
                        VersionComment = dto.VersionComment ?? "",
                        IsActive = true
                    };
                    _db.ProductVersions.Add(newVer);
                    await _db.SaveChangesAsync();

                    // === COPY TECHNOLOGY (parts + steps) ===
if (dto.CopyTechnologySteps && prev is not null)
{
    // 1) paņem visas aktīvās detaļas no iepriekšējās versijas
var oldParts = await _db.ProductTopParts
    .Where(x => x.VersionId == prev.Id && x.IsActive)
    .OrderBy(x => x.Id)
    .ToListAsync();

// 2) izveido detaļas jaunajai versijai (vienā reizē) + uztaisa map: oldPartId -> newPartId
var newParts = oldParts.Select(op => new ProductTopPart
{
    VersionId = newVer.Id,
    TopPartId = op.TopPartId,
    QtyPerProduct = op.QtyPerProduct,
    IsActive = true
}).ToList();

_db.ProductTopParts.AddRange(newParts);
await _db.SaveChangesAsync();

// EF pēc SaveChanges aizpildīs newParts[i].Id, tāpēc varam uztaisīt map 1:1 pēc indeksiem
var map = oldParts
    .Select((op, i) => new { op.Id, NewId = newParts[i].Id })
    .ToDictionary(x => x.Id, x => x.NewId);


    // 3) nokopē soļus katrai detaļai
    var oldPartIds = oldParts.Select(x => x.Id).ToList();

    var oldSteps = await _db.TopPartSteps
        .Where(s => oldPartIds.Contains(s.ProductToPartId) && s.IsActive)
        .ToListAsync();

    foreach (var os in oldSteps)
    {
        if (!map.TryGetValue(os.ProductToPartId, out var newPartId)) continue;

        _db.TopPartSteps.Add(new TopPartStep
        {
            ProductToPartId = newPartId,
            StepOrder = os.StepOrder,
            StepName = os.StepName,
            StepType = os.StepType,
            WorkCentrId = os.WorkCentrId,
            ParallelGroup = os.ParallelGroup,
            IsMandatory = os.IsMandatory,
            IsFinal = os.IsFinal,
            Comments = os.Comments,
            IsActive = true
        });
    }

    await _db.SaveChangesAsync();
}
// === /COPY TECHNOLOGY ===


                    return Ok(new { product.Id, VersionId = newVer.Id });
                }
                else
                {
                    // labo esošo aktīvo versiju: Rasējums / Komentārs
                    if (!dto.VersionId.HasValue)
                        return BadRequest("VersionId is required when CreateNewVersion = false.");

                    var ver = await _db.ProductVersions
                        .FirstOrDefaultAsync(v => v.Id == dto.VersionId.Value && v.ProductId == product.Id && v.IsActive);

                    if (ver is null) return NotFound("Active version not found.");

                    if (dto.VersionRasejums is not null) ver.VersionRasejums = dto.VersionRasejums;
                    if (dto.VersionComment is not null) ver.VersionComment = dto.VersionComment;

                    await _db.SaveChangesAsync();
                    return Ok(new { product.Id, VersionId = ver.Id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[API UPDATE ERROR] " + ex.ToString());
                return StatusCode(500, "UPDATE failed: " + ex.Message);
            }
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> Delete([FromQuery] int id)
        {
            Console.WriteLine($"[API DELETE] id={id}");
            try
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
                if (product is null)
                    return NotFound("Product not found or already inactive.");

                // 1) pati prece -> neaktīva
                product.IsActive = false;

                // 2) visas šīs preces aktīvās versijas -> neaktīvas
                var versions = await _db.ProductVersions
                    .Where(v => v.ProductId == id && v.IsActive)
                    .ToListAsync();

                foreach (var v in versions)
                    v.IsActive = false;

                await _db.SaveChangesAsync();

                return Ok(new { product.Id, DeactivatedVersions = versions.Count });
            }
            catch (Exception ex)
            {
                Console.WriteLine("[API DELETE ERROR] " + ex.ToString());
                return StatusCode(500, "DELETE failed: " + ex.Message);
            }
        }
        
        [HttpGet("steps-by-part")]
        public async Task<IActionResult> GetStepsByPart([FromQuery] int productToPartId)
        {
            var part = await _db.ProductTopParts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productToPartId && p.IsActive);
            if (part is null) return NotFound("Part not found or inactive.");

            var steps = await _db.TopPartSteps.AsNoTracking()
                .Where(s => s.ProductToPartId == productToPartId && s.IsActive)
                .OrderBy(s => s.StepOrder)
                
                .Join(_db.StepTypes.Where(st => st.IsActive),
                    s => s.StepType,
                    st => st.Id,
                    (s, st) => new { s, StepTypeName = st.StepTypeName })
.Join(_db.WorkCentrs.Where(wc => wc.IsActive),
      t => t.s.WorkCentrId,
      wc => wc.Id,
   (t, wc) => new
{
    t.s.Id,
    t.s.ProductToPartId,
    t.s.StepOrder,
    t.s.StepName,

    StepType = t.s.StepType,         // ← PAREIZI
    StepTypeName = t.StepTypeName,   // ← PAREIZI

    t.s.WorkCentrId,
    EstimatedMinutes = t.s.EstimatedMinutes,
    WorkCenterName = wc.WorkCentr_Name,
    t.s.ParallelGroup,
    t.s.IsMandatory,
    t.s.IsFinal,
    t.s.Comments
})

.ToListAsync();


            return Ok(steps);
        }

        [HttpPost("step")]
        public async Task<IActionResult> CreateStep([FromBody] CreateStepRequest dto)
        {
            Console.WriteLine("CREATE STEP API CALLED");
            if (dto.ProductToPartId <= 0) return BadRequest("ProductToPartId is required.");
            if (string.IsNullOrWhiteSpace(dto.StepName)) return BadRequest("StepName is required.");
           if (dto.WorkCentrId <= 0) return BadRequest("WorkCentrId required.");

            // 1) Part must be active and belong to an ACTIVE version
            var ptp = await _db.ProductTopParts
                .FirstOrDefaultAsync(p => p.Id == dto.ProductToPartId && p.IsActive);

            if (ptp is null)
                return NotFound("Part not found or inactive.");

            var hasTasks = await _db.Tasks
                .AnyAsync(t => 
                    _db.TopPartSteps
                        .Where(s => s.ProductToPartId == ptp.Id)
                        .Select(s => s.Id)
                        .Contains(t.TopPartStep_ID)
                    && t.IsActive);

            if (hasTasks)
                return BadRequest("Nevar pievienot soli – šai detaļai jau ir uzdevumi.");

            // papildus pārbaudām, vai saistītā versija ir aktīva
            var versionActive = await _db.ProductVersions
                .AnyAsync(v => v.Id == ptp.VersionId && v.IsActive);

            if (!versionActive)
                return BadRequest("Steps can be edited only for active version.");
        // ja UI nav atsūtījis StepType (vai 0) -> piešķiram pēc TopPart.Stage
if (dto.StepType <= 0)
{
    var stage = await _db.TopParts
        .Where(tp => tp.Id == ptp.TopPartId && tp.IsActive)
        .Select(tp => tp.Stage)
        .FirstOrDefaultAsync();

dto.StepType = (int)await _db.StageStepTypeMaps
    .Where(m => m.IsActive && m.Stage == stage)
    .Select(m => m.Step_Type_ID)
    .FirstOrDefaultAsync();

    if (dto.StepType <= 0)
        return BadRequest($"Nav konfigurēts StepType priekš Stage={stage}.");
}

            // 2) StepOrder: ja 0, piešķiram max+10
            if (dto.StepOrder == 0)
            {
                var maxOrder = await _db.TopPartSteps
                    .Where(s => s.ProductToPartId == dto.ProductToPartId && s.IsActive)
                    .Select(s => (int?)s.StepOrder)
                    .MaxAsync() ?? 0;
                dto.StepOrder = maxOrder + 10;
            }

            // 3) ParallelGroup default 1


            // 4) IsFinal – nodrošinām, ka nebūs 2 aktīvi finālie
                if (dto.IsFinal)
                {
                    var others = await _db.TopPartSteps
                        .Where(s => s.ProductToPartId == dto.ProductToPartId && s.IsActive && s.IsFinal)
                        .ToListAsync();

                    foreach (var o in others)
                        o.IsFinal = false;
                }

            var step = new TopPartStep
            {
                ProductToPartId = dto.ProductToPartId,
                StepOrder = dto.StepOrder,
                StepName = dto.StepName,
                StepType = dto.StepType,
                WorkCentrId = dto.WorkCentrId,
                EstimatedMinutes = dto.EstimatedMinutes,
                ParallelGroup = dto.ParallelGroup,
                IsMandatory = dto.IsMandatory,
                IsFinal = dto.IsFinal,
                Comments = dto.Comments ?? "",
                IsActive = true
            };

            _db.TopPartSteps.Add(step);
            await _db.SaveChangesAsync();

            return Ok(new { step.Id });
        }

        [HttpPut("step")]
        public async Task<IActionResult> UpdateStep([FromBody] UpdateStepRequest dto)
        {
            if (dto.Id <= 0) return BadRequest("Id is required.");
            if (string.IsNullOrWhiteSpace(dto.StepName)) return BadRequest("StepName is required.");
           if (dto.WorkCentrId <= 0) return BadRequest("WorkCentrId required.");

            var step = await _db.TopPartSteps.FirstOrDefaultAsync(s => s.Id == dto.Id && s.IsActive);
            if (step is null) return NotFound("Step not found or inactive.");

            // Only active version can be edited
            var ptp = await _db.ProductTopParts.FirstOrDefaultAsync(p => p.Id == step.ProductToPartId && p.IsActive);

            if (ptp is null) return BadRequest("Part is inactive.");
            var versionActive = await _db.ProductVersions.AnyAsync(v => v.Id == ptp.VersionId && v.IsActive);
            if (!versionActive) return BadRequest("Steps can be edited only for active version.");

if (dto.StepType <= 0)
{
    var stage = await _db.TopParts
        .Where(tp => tp.Id == ptp.TopPartId && tp.IsActive)
        .Select(tp => tp.Stage)
        .FirstOrDefaultAsync();

dto.StepType = (int)await _db.StageStepTypeMaps
    .Where(m => m.IsActive && m.Stage == stage)
    .Select(m => m.Step_Type_ID)
    .FirstOrDefaultAsync();

    if (dto.StepType <= 0)
        return BadRequest($"Nav konfigurēts StepType priekš Stage={stage}.");
}
            // StepOrder
            if (dto.StepOrder <= 0) dto.StepOrder = step.StepOrder;

            // ParallelGroup default 1

            // IsFinal: ja uzliekam true — jāpārliecinās, ka citiem nav final
            if (dto.IsFinal && !step.IsFinal)
            {
                var others = await _db.TopPartSteps
                    .Where(s => s.ProductToPartId == step.ProductToPartId && s.IsActive && s.Id != step.Id && s.IsFinal)
                    .ToListAsync();

                foreach (var o in others)
                    o.IsFinal = false;
            }

            step.StepOrder = dto.StepOrder;
            step.StepName = dto.StepName;
            step.StepType = dto.StepType;
            step.WorkCentrId = dto.WorkCentrId;
            step.EstimatedMinutes = dto.EstimatedMinutes;
            step.ParallelGroup = dto.ParallelGroup;
            step.IsMandatory = dto.IsMandatory;
            step.IsFinal = dto.IsFinal;
            step.Comments = dto.Comments ?? "";

            await _db.SaveChangesAsync();
            return Ok(new { step.Id });
        }

        [HttpDelete("step/{id}")]
        public async Task<IActionResult> DeleteStep([FromRoute] int id)
        {
            var step = await _db.TopPartSteps.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
            if (step is null) return NotFound();

            // tikai aktīvai versijai
            var ptp = await _db.ProductTopParts.FirstOrDefaultAsync(p => p.Id == step.ProductToPartId && p.IsActive);
            if (ptp is null) return BadRequest("Part is inactive.");
            var versionActive = await _db.ProductVersions.AnyAsync(v => v.Id == ptp.VersionId && v.IsActive);
            if (!versionActive) return BadRequest("Steps can be edited only for active version.");

            var hasTasks = await _db.Tasks
                .AnyAsync(t => t.TopPartStep_ID == step.Id && t.IsActive);

            if (hasTasks)
                return BadRequest("Šo soli nevar dzēst – tam jau ir izveidoti uzdevumi.");

            step.IsActive = false;

// pārrēķinām secību pēc dzēšanas
        var steps = await _db.TopPartSteps
            .Where(s => s.ProductToPartId == step.ProductToPartId && s.IsActive && s.Id != step.Id)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();

        for (int i = 0; i < steps.Count; i++)
        {
            steps[i].StepOrder = (i + 1) * 10;
        }

            await _db.SaveChangesAsync();

                // nodrošinām, ka pēdējais solis ir Final
                var last = steps.LastOrDefault();
                if (last != null)
                {
                    foreach (var s in steps)
                        s.IsFinal = false;

                    last.IsFinal = true;

                    await _db.SaveChangesAsync();
                }

            return Ok();
        }

        [HttpGet("/api/workcenters")]
        public async Task<IActionResult> GetWorkCenters()
        {
            var rows = await _db.WorkCentrs
                .AsNoTracking()
                .Where(wc => wc.IsActive)
                .OrderBy(wc => wc.WorkCenter_Order)
                .Select(wc => new
                {
                    wc.Id,
                    wc.WorkCentr_Name,
                    wc.WorkCenter_Order
                })
                .ToListAsync();

            return Ok(rows);
        }

          // DTO
        public sealed class AddPartRequest
        {
            public int productId { get; set; }      // Produkta Id
            public int topPartId { get; set; }      // Detaļas Id
            public int qtyPerProduct { get; set; }  // Vesels skaitlis >=1
        }

        [HttpPost("add-part")]
        public async Task<IActionResult> AddPart([FromBody] AddPartRequest dto, [FromServices] AppDbContext db)
        {
            if (dto.productId <= 0 || dto.topPartId <= 0 || dto.qtyPerProduct < 1)
                return BadRequest(new { message = "Nepareizi parametri (productId, topPartId vai qtyPerProduct)." });

            // 1) Aktīvā versija šai precei
            var versionId = await db.ProductVersions
                .Where(v => v.ProductId == dto.productId && v.IsActive)
                .Select(v => v.Id)
                .SingleOrDefaultAsync();

            if (versionId == 0)
                return BadRequest(new { message = "Šai precei nav aktīvas versijas." });

            // 2) Pārbaudām detaļu
            var topPartExists = await db.TopParts.AnyAsync(tp => tp.Id == dto.topPartId && tp.IsActive);
            if (!topPartExists)
                return BadRequest(new { message = "Detaļa (TopPart) nav aktīva vai neeksistē." });

            // 3) Apstrādājam dublikātu (ignorējot IsActive, jo DB indeksam tas nav iekļauts)
var existing = await db.ProductTopParts
    .FirstOrDefaultAsync(p => p.VersionId == versionId && p.TopPartId == dto.topPartId);

if (existing is not null)
{
    if (!existing.IsActive)
    {
        // reaktivējam esošo rindu, lai neizsauktu DB unikālā indeksa kļūdu
        existing.IsActive = true;
        existing.QtyPerProduct = dto.qtyPerProduct;
        await db.SaveChangesAsync();
        return Ok(new { id = existing.Id, reactivated = true });
        
    }

    return Conflict(new { message = "Šī detaļa jau ir pievienota aktīvajai versijai." });
}

// 4) Izveidojam saiti “versija -> detaļa”
var row = new ProductTopPart
{
    VersionId = versionId,
    TopPartId = dto.topPartId,
    QtyPerProduct = dto.qtyPerProduct,
    IsActive = true
};

db.ProductTopParts.Add(row);
await db.SaveChangesAsync();
return Ok(new { id = row.Id });

        }

        // soft delete 
        [HttpDelete("delete-part/{id:int}")]
        public async Task<IActionResult> DeletePart(int id, [FromServices] AppDbContext db)
        {
            var link = await db.ProductTopParts.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (link == null) return NotFound(new { message = "Saite nav atrasta vai jau neaktīva." });

            link.IsActive = false;
            await db.SaveChangesAsync();
            return Ok();
        }

        // === DTO pievienošanai ===
        public class AddPartDto
        {
            public int VersionId { get; set; }
            public int TopPartId { get; set; }
            public int QtyPerProduct { get; set; }
        }

        // DTO
        public sealed class StepTypeRequest { public int Id { get; set; } public string? Name { get; set; } }

        // GET dropdownam – tikai aktīvie
        [HttpGet("/api/steptypes")]
        public async Task<IActionResult> GetActiveStepTypes([FromServices] AppDbContext db)
        {
            var rows = await db.StepTypes
                .Where(x => x.IsActive)
                .OrderBy(x => x.StepTypeName)
                .Select(x => new { x.Id, x.StepTypeName })
                .ToListAsync();
            return Ok(rows);
        }

        // GET pārvaldībai – visi (ar aktīvo statusu)
        [HttpGet("/api/steptypes/manage")]
        public async Task<IActionResult> GetAllStepTypes([FromServices] AppDbContext db)
        {
            var rows = await db.StepTypes
                .OrderByDescending(x => x.IsActive).ThenBy(x => x.StepTypeName)
                .Select(x => new { x.Id, x.StepTypeName, x.IsActive })
                .ToListAsync();
            return Ok(rows);
        }

        // POST – izveidot (IsActive = true)
        [HttpPost("/api/steptypes")]
        public async Task<IActionResult> CreateStepType([FromBody] StepTypeRequest dto, [FromServices] AppDbContext db)
        {
            var name = (dto.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Nosaukums ir obligāts.");
            var exists = await db.StepTypes.AnyAsync(x => x.StepTypeName == name && x.IsActive);
            if (exists) return Conflict("Šāds nosaukums jau eksistē.");

            db.StepTypes.Add(new ManiApi.Models.StepType { StepTypeName = name, IsActive = true });
            await db.SaveChangesAsync();
            return Ok();
        }

        // PUT – pārdēvēt
        [HttpPut("/api/steptypes")]
        public async Task<IActionResult> RenameStepType([FromBody] StepTypeRequest dto, [FromServices] AppDbContext db)
        {
            var row = await db.StepTypes.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (row is null) return NotFound();
            var name = (dto.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("Nosaukums ir obligāts.");

            var exists = await db.StepTypes.AnyAsync(x => x.Id != dto.Id && x.StepTypeName == name && x.IsActive);
            if (exists) return Conflict("Šāds nosaukums jau eksistē.");

            row.StepTypeName = name;
            await db.SaveChangesAsync();
            return Ok();
        }

        // DELETE – soft delete (IsActive=false)
        [HttpDelete("/api/steptypes/{id:int}")]
        public async Task<IActionResult> DeleteStepType(int id, [FromServices] AppDbContext db)
        {
            var row = await db.StepTypes.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (row is null) return NotFound();
            row.IsActive = false;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("/api/workcenters/manage")]
        public async Task<IActionResult> GetAllWorkCenters([FromServices] AppDbContext db)
        {
            var rows = await db.WorkCentrs
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.WorkCenter_Order)
                .Select(x => new 
                        { 
                            x.Id, 
                            x.WorkCentr_Name, 
                            x.WorkCenter_Order,
                            x.Step_Type_ID,
                            x.IsActive 
                        })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpPost("/api/workcenters/add")]
        public async Task<IActionResult> AddWorkCenter([FromServices] AppDbContext db, [FromBody] WorkCenter dto)
        {
           if (string.IsNullOrWhiteSpace(dto.WorkCentr_Name))
    return BadRequest("Nosaukums ir obligāts.");

// ja UI nav atsūtījis kodu, ģenerē to no nosaukuma
if (string.IsNullOrWhiteSpace(dto.WorkCentr_Code))
{
    dto.WorkCentr_Code = (dto.WorkCentr_Name ?? "")
        .Trim()
        .ToUpper()
        .Replace(" ", "_");
}

var maxOrder = await db.WorkCentrs.MaxAsync(x => (int?)x.WorkCenter_Order) ?? 0;
dto.WorkCenter_Order = maxOrder + 10;

if (dto.Step_Type_ID == null)
    dto.Step_Type_ID = 1;

dto.IsActive = true;
db.WorkCentrs.Add(dto);
await db.SaveChangesAsync();

return Ok(dto);

        }

        [HttpPut("/api/workcenters/update")]
        public async Task<IActionResult> UpdateWorkCenter(
            [FromServices] AppDbContext db,
            [FromBody] WorkCenter dto)
        {
            if (dto.Id <= 0) return BadRequest("Trūkst ID.");
            if (string.IsNullOrWhiteSpace(dto.WorkCentr_Name))
                return BadRequest("Nosaukums ir obligāts.");

            var row = await db.WorkCentrs.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (row is null) return NotFound();

            row.WorkCentr_Name = dto.WorkCentr_Name;
            if (dto.WorkCenter_Order <= 0)
                {
                    var maxOrder = await db.WorkCentrs.MaxAsync(x => (int?)x.WorkCenter_Order) ?? 0;
                    row.WorkCenter_Order = maxOrder + 10;
                }
                else
                {
                    row.WorkCenter_Order = dto.WorkCenter_Order;
                }
            row.Step_Type_ID = dto.Step_Type_ID;
            await db.SaveChangesAsync();
            return Ok();
        }

[HttpDelete("/api/workcenters/{id:int}")]
public async Task<IActionResult> SoftDeleteWorkCenter(int id, [FromServices] AppDbContext db)
{
    var row = await db.WorkCentrs.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
    if (row is null) return NotFound();

    row.IsActive = false;
    await db.SaveChangesAsync();
    return Ok();
}

[HttpGet("planning-list")]
public async Task<IActionResult> GetPlanningList()
{
    var cs = _db.Database.GetConnectionString();
    await using var conn = new MySqlConnection(cs);
    await conn.OpenAsync();
    
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT DISTINCT
    p.ID            AS Id,
    p.Product_Code  AS ProductCode,
    p.Product_Name  AS ProductName,
    p.Category_ID   AS CategoryId,
    c.Parent_ID     AS ParentCategoryId,
    c.Category_Name AS CategoryName,
    CASE 
        WHEN c.Parent_ID IS NULL THEN c.Category_Name
        ELSE pc.Category_Name
    END             AS RootName,
    v.ID            AS Version_Id,
    v.Version_Name  AS VersionName,
    v.Version_Date  AS VersionDate
FROM versions v
JOIN products p      ON p.ID = v.Product_ID AND p.IsActive = 1
JOIN categories c    ON c.ID = p.Category_ID AND c.IsActive = 1
LEFT JOIN categories pc ON pc.ID = c.Parent_ID AND pc.IsActive = 1
WHERE
    v.IsActive = 1
    OR v.ID IN (
        -- 1) WIP: versija ir partijās (aktīvas, status=1)
        SELECT bp.Version_Id
        FROM batches_products bp
        JOIN batches b ON b.ID = bp.Batch_Id
        WHERE bp.IsActive = 1
          AND b.IsActive  = 1
          AND b.Batches_Statuss = 1

        UNION

        -- 2) Noliktavas atlikums: jebkāds STOCK kustību atlikums > 0
        SELECT bp.Version_Id
        FROM stock_movements sm
        JOIN batches_products bp ON bp.ID = sm.BatchProduct_ID
        JOIN batches b ON b.ID = bp.Batch_Id
        WHERE sm.IsActive = 1
          AND bp.IsActive = 1
          AND b.IsActive  = 1
        GROUP BY bp.Version_Id
        HAVING SUM(CASE WHEN sm.Move_Type = 'STOCK' THEN sm.Stock_Qty ELSE 0 END) > 0
    )
ORDER BY RootName, CategoryName, ProductName, VersionDate DESC;
";

var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
            {
                Id = r.GetInt32(0),
                ProductCode = r.GetString(1),
                ProductName = r.GetString(2),
                CategoryId = r.GetInt32(3),
                ParentCategoryId = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
                CategoryName = r.IsDBNull(5) ? "" : r.GetString(5),
                RootName = r.IsDBNull(6) ? "" : r.GetString(6),
                VersionId = r.GetInt32(7),
                VersionName = r.IsDBNull(8) ? null : r.GetString(8),
                VersionDate = r.IsDBNull(9) ? null : r.GetValue(9)?.ToString()
            });
    }

    return Ok(list);
}

public sealed class SetPriorityRequest
{
    public int VersionId { get; set; }
    public bool IsPriority { get; set; }
}

[HttpPut("set-priority")]
public async Task<IActionResult> SetPriority([FromBody] SetPriorityRequest dto)
{
    if (dto.VersionId <= 0)
        return BadRequest("VersionId is required.");

    var version = await _db.ProductVersions
        .FirstOrDefaultAsync(v => v.Id == dto.VersionId);

    if (version is null)
        return NotFound("Version not found.");

    version.IsPriority = dto.IsPriority;
    await _db.SaveChangesAsync();

    return Ok(new
    {
        version.Id,
        version.IsPriority
    });
}

[HttpPut("toggle-part")]
public async Task<IActionResult> TogglePart([FromBody] TogglePartRequest dto)
{
    var entity = await _db.ProductTopParts
        .FirstOrDefaultAsync(x => x.Id == dto.ProductToPartId);

    if (entity is null)
        return NotFound("Ieraksts nav atrasts.");

    // 1️⃣ mainām pašas detaļas statusu
    entity.IsActive = dto.IsActive;

    await _db.SaveChangesAsync();

    return Ok();
}

public class TogglePartRequest
{
    public int ProductToPartId { get; set; }
    public bool IsActive { get; set; }
}

public class ProductListItemDto
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string? RootName { get; set; }
    public int VersionId { get; set; }
    public string? VersionName { get; set; }
    public DateOnly? VersionDate { get; set; }
    public bool VersionIsActive { get; set; }
    public bool IsPriority { get; set; }

    public int GroupType { get; set; }  
    public bool IsActive { get; set; } 
}

[HttpGet("/api/stage-step-map")]
public async Task<IActionResult> GetStageStepMap(
    [FromServices] AppDbContext db)
{
    var rows = await db.StageStepTypeMaps
        .Where(x => x.IsActive)
        .Select(x => new
        {
            x.Stage,
            x.Step_Type_ID
        })
        .ToListAsync();

    return Ok(rows);
}


    } // ← beidzas klase ProductsController
    
} // ← beidzas namespace


