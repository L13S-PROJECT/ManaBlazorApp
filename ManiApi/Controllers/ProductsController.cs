// Preču API kontrolieris
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

    // 2️⃣ atrodam visus šīs detaļas soļus
    var steps = await _db.TopPartSteps
        .Where(s => s.ProductToPartId == entity.Id)
        .ToListAsync();

    // 3️⃣ sinhronizējam soļus ar detaļas statusu
    foreach (var step in steps)
    {
        step.IsActive = dto.IsActive;
    }

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


