using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    public ReportsController(IHttpClientFactory httpClientFactory)
        => _httpClientFactory = httpClientFactory;

    [HttpGet("planning-excel")]
    public async Task<IActionResult> ExportPlanningExcel()
    {
        var http = _httpClientFactory.CreateClient();

        // 1) No Products API paņemam rootName pēc productCode (čotka no DB)
        var products = await http.GetFromJsonAsync<List<ProductInfo>>(
            "http://localhost:5270/api/products/list")
            ?? new List<ProductInfo>();

        var rootByCode = products
            .Where(p => !string.IsNullOrWhiteSpace(p.ProductCode))
            .GroupBy(p => p.ProductCode!)
            .ToDictionary(g => g.Key, g => (g.First().RootName ?? "").Trim());

        // 2) No Batches/list paņemam planning skaitļus
        var data = await http.GetFromJsonAsync<List<PlanningRow>>(
            "http://localhost:5270/api/Batches/list")
            ?? new List<PlanningRow>();

            var planByCode = data
    .Where(d => !string.IsNullOrWhiteSpace(d.ProductCode))
    .GroupBy(d => d.ProductCode.Trim())
    .ToDictionary(g => g.Key, g => new
    {
        Planned = g.Sum(x => x.Planned),
        DetailedInProgress = g.Sum(x => x.DetailedInProgress),
        DetailedFinish = g.Sum(x => x.DetailedFinish),
        AssemblyInProgress = g.Sum(x => x.AssemblyInProgress),
        AssemblyFinish = g.Sum(x => x.AssemblyFinish),
        FinishingInProgress = g.Sum(x => x.FinishingInProgress),
    });


        // 3) Excel
        using var wb = new XLWorkbook();
        var wsKauss = wb.Worksheets.Add("KAUSS");
        var wsAdapteris = wb.Worksheets.Add("ADAPTERIS");

        var headers = new[]
        {
            "Nosaukums","Kods",
            "InStock","Planned",
            "DetailedInProgress","DetailedFinish",
            "AssemblyINProgress","AssemblyFinish",
            "FinishingInProgress"
        };

int WriteGrouped(IXLWorksheet ws, IEnumerable<ProductInfo> list)
{
    int r = 2;
    string? lastCategory = null;

    foreach (var p in list.OrderBy(x => x.CategoryName).ThenBy(x => x.ProductName))
    {
        // pelēkā kategorijas rinda
        if (!string.Equals(lastCategory, p.CategoryName, StringComparison.OrdinalIgnoreCase))
        {
            lastCategory = p.CategoryName ?? "";

            ws.Cell(r, 1).Value = lastCategory;

            var catRange = ws.Range(r, 1, r, 9);  // 9 kolonnas
            catRange.Merge();
            catRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#d9d9d9");
            catRange.Style.Font.Bold = true;
            catRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            r++;
        }

      planByCode.TryGetValue((p.ProductCode ?? "").Trim(), out var x);

ws.Cell(r, 1).Value = p.ProductName;
ws.Cell(r, 2).Value = p.ProductCode;

ws.Cell(r, 3).Value = 0; // InStock pagaidām
ws.Cell(r, 4).Value = x?.Planned ?? 0;
ws.Cell(r, 5).Value = x?.DetailedInProgress ?? 0;
ws.Cell(r, 6).Value = x?.DetailedFinish ?? 0;
ws.Cell(r, 7).Value = x?.AssemblyInProgress ?? 0;
ws.Cell(r, 8).Value = x?.AssemblyFinish ?? 0;
ws.Cell(r, 9).Value = x?.FinishingInProgress ?? 0;


        r++;
    }

    return r - 1; // pēdējā aizpildītā rinda
}

        void WriteHeaders(IXLWorksheet ws)
        {
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            var hr = ws.Range(1, 1, 1, headers.Length);
            hr.Style.Font.Bold = true;
            hr.Style.Fill.BackgroundColor = XLColor.LightGray;
            hr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        WriteHeaders(wsKauss);
        WriteHeaders(wsAdapteris);
       
var kaussList = products
    .Where(p => (p.RootName ?? "").Equals("Kauss", StringComparison.OrdinalIgnoreCase));

var adapterList = products
    .Where(p => (p.RootName ?? "").Equals("Adapteri", StringComparison.OrdinalIgnoreCase));


int lastRowKauss = WriteGrouped(wsKauss, kaussList);
int lastRowAd    = WriteGrouped(wsAdapteris, adapterList);

        // 6) Krāsošana (viena funkcija abiem sheet)
        void ApplyStageColors(IXLWorksheet ws, int lastRow)
        {
            if (lastRow < 2) return;

            void ColorHeader(int col, string htmlColor)
            {
                var h = ws.Cell(1, col);
                h.Style.Fill.BackgroundColor = XLColor.FromHtml(htmlColor);
                h.Style.Font.Bold = true;
                h.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            void ColorCellsIfPositive(int col, string htmlColor)
            {
                var rng = ws.Range(2, col, lastRow, col);
                rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                foreach (var cell in rng.Cells())
                {
                    if (!cell.TryGetValue<double>(out var n)) continue;
                    if (n > 0) cell.Style.Fill.BackgroundColor = XLColor.FromHtml(htmlColor);
                }
            }

            // header 1-2
ColorHeader(1, "#d3d3d3");
ColorHeader(2, "#d3d3d3");

// InStock (3) — visa kolonna
ColorHeader(3, "#d9d9d9");
ws.Range(2, 3, lastRow, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#d9d9d9");
ws.Range(2, 3, lastRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

// posmi (4..9)
ColorHeader(4, "#00ff00");  ColorCellsIfPositive(4, "#00ff00");   // Planned
ColorHeader(5, "#f9b115");  ColorCellsIfPositive(5, "#f9b115");   // DetailedInProgress
ColorHeader(6, "#90ee90");  ColorCellsIfPositive(6, "#90ee90");   // DetailedFinish
ColorHeader(7, "#f9b115");  ColorCellsIfPositive(7, "#f9b115");   // AssemblyINProgress
ColorHeader(8, "#2eb85c");  ColorCellsIfPositive(8, "#2eb85c");   // AssemblyFinish
ColorHeader(9, "#ffe873");  ColorCellsIfPositive(9, "#ffe873");   // FinishingInProgress

        }
           

ApplyStageColors(wsKauss, lastRowKauss);
ApplyStageColors(wsAdapteris, lastRowAd);


        // 7) “Skaistums”
        wsKauss.Columns().AdjustToContents();
        wsKauss.SheetView.FreezeRows(1);

        wsAdapteris.Columns().AdjustToContents();
        wsAdapteris.SheetView.FreezeRows(1);

        // 8) Atgriežam failu
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Planning_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        );
    }

    // ====== DTO ======

    public sealed class PlanningRow
    {
        public int VersionId { get; set; }
        public string ProductName { get; set; } = "";
        public string ProductCode { get; set; } = "";

        public int Planned { get; set; }
        public int DetailedInProgress { get; set; }
        public int DetailedFinish { get; set; }

        // /api/Batches/list atgriež Assembly un Done - bet Excel vajag 2 kolonnu nosaukumus
        public int Assembly { get; set; }              // ja tev API tieši tā sūta
        public int Done { get; set; }                  // ja tev API tieši tā sūta

        public int FinishingInProgress { get; set; }

        // ērtībai Excel mapping:
        public int AssemblyInProgress => Assembly;
        public int AssemblyFinish => Done;
    }

    public sealed class ProductInfo
{
    public int Id { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? CategoryName { get; set; }
    public string? RootName { get; set; }
}

}
