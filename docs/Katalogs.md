1) izveidots saraksts Versija DB:

CREATE TABLE `versions` (
	`id` INT(11) NOT NULL AUTO_INCREMENT,
	`product_id` INT(11) NOT NULL,
	`version_name` VARCHAR(50) NOT NULL COLLATE 'utf8mb4_general_ci',
	`version_rasejums` VARCHAR(100) NULL DEFAULT NULL COLLATE 'utf8mb4_general_ci',
	`version_date` DATE NOT NULL,
	`version_comment` VARCHAR(250) NULL DEFAULT NULL COLLATE 'utf8mb4_general_ci',
	`IsActive` TINYINT(4) NOT NULL DEFAULT '1',
	`is_priority` TINYINT(1) NOT NULL DEFAULT '0',
	`priority_target_date` DATE NULL DEFAULT NULL,
	PRIMARY KEY (`id`) USING BTREE,
	INDEX `Index 2` (`product_id`, `IsActive`) USING BTREE,
	CONSTRAINT `FK__products` FOREIGN KEY (`product_id`) REFERENCES `products` (`ID`) ON UPDATE NO ACTION ON DELETE CASCADE
)
COLLATE='utf8mb4_general_ci'
ENGINE=InnoDB
AUTO_INCREMENT=64
;

2) savienots ar topproduct - producttoppart

CREATE TABLE `producttopparts` (
	`ID` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	`Version_ID` INT(11) NOT NULL,
	`TopPart_ID` INT(11) UNSIGNED NOT NULL,
	`Qty_Per_product` INT(11) UNSIGNED NOT NULL DEFAULT '1',
	`IsActive` TINYINT(1) NOT NULL DEFAULT '1',
	PRIMARY KEY (`ID`) USING BTREE,
	UNIQUE INDEX `Index 5` (`TopPart_ID`, `Version_ID`) USING BTREE,
	INDEX `FK_producttopparts_versions` (`Version_ID`) USING BTREE,
	CONSTRAINT `FK_producttopparts_versions` FOREIGN KEY (`Version_ID`) REFERENCES `versions` (`id`) ON UPDATE CASCADE ON DELETE RESTRICT,
	CONSTRAINT `FK_toppartsteps_toppart` FOREIGN KEY (`TopPart_ID`) REFERENCES `toppart` (`ID`) ON UPDATE CASCADE ON DELETE RESTRICT
)
COLLATE='utf8mb4_general_ci'
ENGINE=InnoDB
AUTO_INCREMENT=122
;

3) savienots ar toppartsteps

CREATE TABLE `toppartsteps` (
	`ID` INT(11) UNSIGNED NOT NULL AUTO_INCREMENT,
	`ProductToPart_ID` INT(11) UNSIGNED NOT NULL,
	`Step_Order` INT(11) UNSIGNED NOT NULL DEFAULT '10' COMMENT 'Darba secības numurs (10,20,30 …)',
	`Step_Name` VARCHAR(255) NOT NULL COLLATE 'utf8mb4_general_ci',
	`Step_Type` INT(11) UNSIGNED NOT NULL,
	`Parallel_Group` INT(11) UNSIGNED NULL DEFAULT NULL COMMENT 'Vienlaicīgi izpildāmo soļu grupa',
	`IsFinal` TINYINT(1) UNSIGNED NULL DEFAULT '0',
	`IsMandatory` TINYINT(1) UNSIGNED NOT NULL DEFAULT '1' COMMENT 'Vai solis ir obligāts',
	`WorkCentr_ID` INT(11) UNSIGNED NULL DEFAULT NULL COMMENT 'Atsauce uz workcenters tabulu',
	`Estimated_Minutes` INT(10) UNSIGNED NULL DEFAULT NULL COMMENT 'Aptuvenais laiks minūtēs vienam gabalam šajā solī',
	`Comments` VARCHAR(500) NULL DEFAULT NULL COLLATE 'utf8mb4_general_ci',
	`IsActive` TINYINT(1) UNSIGNED NOT NULL DEFAULT '1',
	PRIMARY KEY (`ID`) USING BTREE,
	INDEX `Step_Type` (`Step_Type`) USING BTREE,
	INDEX `WorkCentr` (`WorkCentr_ID`) USING BTREE,
	INDEX `Index 5` (`ProductToPart_ID`, `IsActive`, `Step_Order`) USING BTREE,
	CONSTRAINT `Step_Type` FOREIGN KEY (`Step_Type`) REFERENCES `step_type` (`ID`) ON UPDATE CASCADE ON DELETE RESTRICT,
	CONSTRAINT `WorkCentr` FOREIGN KEY (`WorkCentr_ID`) REFERENCES `workcentr_type` (`ID`) ON UPDATE CASCADE ON DELETE RESTRICT,
	CONSTRAINT `producttopparts` FOREIGN KEY (`ProductToPart_ID`) REFERENCES `producttopparts` (`ID`) ON UPDATE CASCADE ON DELETE RESTRICT
)
COLLATE='utf8mb4_general_ci'
ENGINE=InnoDB
AUTO_INCREMENT=194
;


4) savienot map stage + step_type

CREATE TABLE `stage_step_type_map` (
	`Stage` TINYINT(4) NOT NULL,
	`Step_Type_ID` INT(11) UNSIGNED NOT NULL,
	`IsActive` TINYINT(1) UNSIGNED NOT NULL DEFAULT '1',
	PRIMARY KEY (`Stage`) USING BTREE,
	INDEX `IX_StepType` (`Step_Type_ID`) USING BTREE,
	CONSTRAINT `FK_stage_step_type_map_step_type` FOREIGN KEY (`Step_Type_ID`) REFERENCES `step_type` (`ID`) ON UPDATE CASCADE ON DELETE RESTRICT
)
COLLATE='utf8mb4_general_ci'
ENGINE=InnoDB
;

5) izveidot tehnoloģijas soļi aktīvajai preces versijai - API ProductControllers.cs

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


Products.razor
@page "/products"
@using System.Net.Http.Json
@using System.Linq
@using ManaApp.Models
@using Syncfusion.Blazor.Grids
@using Syncfusion.Blazor.Popups
@using Syncfusion.Blazor.DropDowns
@using Syncfusion.Blazor.Inputs
@using Microsoft.JSInterop
@using ManaApp.Shared
@inject HttpClient Http
@inject IJSRuntime JS
@layout MainLayout
@using ManaApp.Components.Products
@using ManaApp.Pages
@using System.Text.Json;     

<div class="products-main">

        <!-- vidējā zona: pa kreisi saraksts, pa labi 3 bloki -->
    <div class="products-content">

            <!-- KREISĀ PUSE: filtri + grida saraksts -->
        <div class="products-main-block">

                <div class="products-section-header">
                    
                    <div class="products-header-title">
                        Preču saraksts
                    </div>

                    <label class="archived-switch">
                        <input type="checkbox" @bind="ShowArchived" />
                        <span class="slider"></span>
                    </label>

                </div>

                <ProductsFilters
                    Categories="filteredCategories"
                    HasFilters="HasAnyFilters"
                    SelectedRoot="@selectedRoot"
                    OnRootChanged="SetRootFilter"
                    OnCategoryChanged="OnCategoryFilterChanged"
                    OnSearch="OnSearchInput"
                    OnClear="ClearFilters"
                    SearchText="@searchText"
                    SearchTextChanged="@(v => searchText = v)"
                    OnNew="OpenDialog"
                    OnEdit="OpenEditDialog"
                    OnDelete="SoftDeleteSelected"
                    CanEdit="@(selected is not null && selectedDetails is not null)"
                    CanDelete="@(selected is not null)">
                </ProductsFilters>

                        <ProductsGrid
                        Rows="rows"
                        OnRowSelected="Grid_RowSelected" />
        </div>



            <!-- LABĀ PUSE: Preces saturs + Tehnoloģija -->
    <div class="products-right">

<ProductContentBox
    Selected="selected"
    Details="selectedDetails"
    Loading="loadingDetails"
    IsReadOnly="@(selected is not null && (!selected.IsActive || !selected.VersionIsActive))" />

            <!-- Tehnoloģija -->

<ProductTechnologyBox
    Selected="selected"
    IsReadOnly="@(selected is not null && (!selected.IsActive || !selected.VersionIsActive))"
    LoadingPartDetails="loadingPartDetails"
    LoadingWorks="loadingWorks"
    SelectedPartDetails="selectedPartDetails"
    WorksByParts="worksByParts"
    AllTopParts="topParts"
    OnAddPart="OpenNewPartDialog"
    OnOpenSteps="OpenStepsForPart"
    OnOpenStepTypes="OpenStepTypeDialogAsync"
    OnOpenWorkCenters="OpenWorkCenterDialogAsync"
    OnDeletePart="DeletePartByCode"
    OnToggleTopPart="ToggleTopPartAsync">
</ProductTechnologyBox>

    </div>
</div>
</div>

<!-- === PRECES dialogs (JAUNS/LABOT) === -->
<NewProductDialog
    Visible="IsProductDialogOpen"
    VisibleChanged="@(v => SetMainDialog(v ? "Product" : null))"
    Header="@(isEdit ? "Labot preci" : "Jauna prece")"
    IsEdit="isEdit"
    ShowValidation="showValidation"
    ParentOptions="parentOptions"
    ChildOptions="childOptions"
    SelectedParentId="selectedParentId"
    SelectedParentIdChanged="@(v => selectedParentId = v)"
    SelectedChildId="selectedChildId"
    SelectedChildIdChanged="@(v => selectedChildId = v)"
    NewName="@newName"
    NewNameChanged="@(v => newName = v)"
    NewCode="@newCode"
    NewCodeChanged="@(v => newCode = v)"
    CreateNewVersion="createNewVersion"
    CreateNewVersionChanged="@(v => createNewVersion = v)"
    CopyTechnologySteps="copyTechnologySteps"
    CopyTechnologyStepsChanged="@(v => SetCopyTechnologySteps(v))"
    NewVersionName="@newVersionName"
    NewVersionNameChanged="@(v => newVersionName = v)"
    NewVersionRasejums="@newVersionRasejums"
    NewVersionRasejumsChanged="@(v => newVersionRasejums = v)"
    NewVersionDate="newVersionDate"
    NewVersionComment="@newVersionComment"
    NewVersionCommentChanged="@(v => newVersionComment = v)"
    OnSave="SaveAsync"
    OnCancel="CloseDialog" />

<!-- === TEHNOLOĢIJAS SOĻI dialogs === -->
<ProductStepsDialog
    Visible="IsStepsOpen"
    VisibleChanged="@(v => SetMainDialog(v ? "Steps" : null))"
    Steps="editingSteps"
    StepTypes="stepTypes"
    WorkCenters="workCenters"
    StepsError="@stepsError"
    OnMoveUp="MoveUp"
    OnMoveDown="MoveDown"
    OnDelete="DeleteStepAsync"
    OnSetFinal="SetFinalStep"
    OnOpenStepTypes="OpenStepTypeDialogAsync"
    OnOpenWorkCenters="OpenWorkCenterDialogAsync"
    OnAddStep="AddStep"
    OnSave="SaveStepsAsync"
    OnClose="CloseStepsDialog" />

<!-- === labots līdz šai vietai === -->

<StepTypesDialog
    Visible="isStepTypesOpen"
    VisibleChanged="@(v => isStepTypesOpen = v)"
    Items="manageStepTypes"
    NewName="@newStepTypeName"
    EditId="@editStepTypeId"
    @bind-EditName="editStepTypeName"
    OnAdd="AddStepTypeAsync"
    OnSaveEdit="SaveEditStepTypeAsync"
    OnCancelEdit="CancelEditStepType"
    OnBeginEdit="BeginEditStepType"
    OnDelete="DeleteStepTypeAsync">
</StepTypesDialog>

<WorkCentersDialog
    Visible="isWorkCentersOpen"
    VisibleChanged="@(v => isWorkCentersOpen = v)"
    Items="manageWorkCenters"
    StepTypes="stepTypes"
    NewName="@newWorkCenterName"
    NewNameChanged="@(v => newWorkCenterName = v)"
    NewCode="@newWorkCenterCode"
    NewCodeChanged="@(v => newWorkCenterCode = v)"
    EditId="editWorkCenterId"
    @bind-EditName="editWorkCenterName"
    OnAdd="AddWorkCenterAsync"
    OnSaveEdit="SaveEditWorkCenterAsync"
    OnCancelEdit="CancelEditWorkCenter"
    OnBeginEdit="BeginEditWorkCenter"
    OnDelete="DeleteWorkCenterAsync"
    MoveUp="MoveUp"
    MoveDown="MoveDown">
</WorkCentersDialog>

<!-- === JAUNA DETAĻA dialogs === -->

<NewPartDialog
    Visible="IsNewPartOpen"
    VisibleChanged="@(v => SetMainDialog(v ? "NewPart" : null))" />

@code {
    // ===== Palīg-īpašības izvēlētajai PRECEI (no 'selected') =====
    private int? SelectedProductId => selected?.id;
    private string SelectedProductLabel =>
        selected is null ? string.Empty : $"{selected.productName} ({selected.productCode})";

    // ===== Modeļi (UI pusē) =====
    private string? activeMainDialog = null;

    private bool IsStepsOpen => activeMainDialog == "Steps";
    private bool IsNewPartOpen => activeMainDialog == "NewPart";

    private bool isStepTypesOpen = false;
    private bool isWorkCentersOpen = false;
    public class CategoryDto
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = "";
        public int? ParentId { get; set; }
        public bool IsActive { get; set; }
    }
    // ===== Stāvoklis =====
    private List<ProductRow> allRows = new();
    private List<ProductRow> rows = new();
    private List<string> parentCategories = new();
    private string selectedRoot = "";
    private string selectedCategory = "";
    private string searchText = "";
    private int filterResetKey = 0;

    private ProductRow? selected;
    private string newWorkCenterCode = "";
    private ProductContentDto? selectedDetails;
    private bool loadingDetails = false;
    private HashSet<string> openParts = new();

    // Režīmi dialogam
    bool isEdit = false;
    private bool _createNewVersion = false;

private bool createNewVersion
{
    get => _createNewVersion;
    set
    {
        _createNewVersion = value;

        // ja noņem “Izveidot jaunu versiju”, tad automātiski noņem arī “Kopēt soļus”
        if (!_createNewVersion)
            copyTechnologySteps = false;
    }
}


    // --- Versijas lauki ---
    private string? newVersionName;
    private string? newVersionRasejums;
    private DateOnly? newVersionDate;
    private string? newVersionComment;

    private List<ProductDetailDto>? selectedPartDetails;
    private bool loadingPartDetails = false;

    private List<WorksByPartDto>? worksByParts;
    private bool loadingWorks = false;

    private List<WorkCenter> workCenters = new();

    // ===== Jaunas preces dialogs =====
    private List<CategoryDto> categories = new();
    private List<CategoryDto> parentOptions => categories.Where(c => c.ParentId == null && c.IsActive).OrderBy(c => c.CategoryName).ToList();
    private List<CategoryDto> childOptions  => categories.Where(c => c.ParentId == selectedParentId && c.IsActive).OrderBy(c => c.CategoryName).ToList();

    private int? selectedParentId;
    private int? selectedChildId;
    private string newName = "";
    private string newCode = "";
    private bool showValidation = false;
    private List<StageStepTypeMapDto> stageStepTypeMap = new();

    public class StageStepTypeMapDto
{
    public int Stage { get; set; }
    public int Step_Type_ID { get; set; }
    public bool IsActive { get; set; }
}

    // ===== Palīg-funkcijas =====
    private void TogglePart(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        if (!openParts.Add(code))
            openParts.Remove(code);
        StateHasChanged();
    }

    // ===== Datu ielāde =====
    protected override async Task OnInitializedAsync()
{
    try
    {
        // 1) ielādē preces sarakstu (minimāli nepieciešamais)
        rows = await Http.GetFromJsonAsync<List<ProductRow>>("http://localhost:5270/api/products/list") 
               ?? new();
        allRows = new List<ProductRow>(rows);

        // 2) ielādē darba centrus (ja 404/500 — turpinām bez tiem)
        try
        {
            workCenters = await Http.GetFromJsonAsync<List<WorkCenter>>("http://localhost:5270/api/workcenters")
                          ?? new();
        }
        catch { workCenters = new List<WorkCenter>(); }

// 2b) ielādē soļu tipus
            try
            {
                stepTypes = await Http.GetFromJsonAsync<List<StepTypeDto>>(
                    "http://localhost:5270/api/steptypes")
                    ?? new();
            }
            catch { stepTypes = new List<StepTypeDto>(); }

// 2c) ielādē stage → stepType mapping
            try
            {
                stageStepTypeMap = await Http.GetFromJsonAsync<List<StageStepTypeMapDto>>(
                    "http://localhost:5270/api/products/stage-step-type-map")
                    ?? new();
            }
            catch { stageStepTypeMap = new List<StageStepTypeMapDto>(); }

        // 3) ielādē kategorijas (ja 404/500 — turpinām bez tām)
        try
        {
            categories = await Http.GetFromJsonAsync<List<CategoryDto>>("http://localhost:5270/api/categories")
                         ?? new();
        }
        catch { categories = new List<CategoryDto>(); }

        // 4) ielādē detaļu (TopPart) izvēlnei (ja 404/500 — turpinām bez tās)
        try
        {
            topParts = await Http.GetFromJsonAsync<List<TopPartDto>>("http://localhost:5270/api/topparts")
                       ?? new();
        }
        catch { topParts = new List<TopPartDto>(); }

        // 5) atjauno filtru skatu
        parentCategories = rows
            .Select(r => r.categoryName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .ToList();

        ApplyFilters(); // lokāla filtrēšana; neuzsāk jaunus HTTP
    }
    catch (Exception ex)
    {
        // nepieļaujam “mūžīgu Loading”
        Console.WriteLine("OnInitializedAsync error: " + ex);
        await JS.InvokeVoidAsync("alert", "Kļūda ielādējot datus: " + ex.Message);
        rows = new(); allRows = new(); // ļaujam lapai atvērties tukšai
    }
}
 

    private IEnumerable<string> filteredCategories =>
        string.IsNullOrWhiteSpace(selectedRoot)
            ? allRows.Select(r => r.categoryName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()
            : allRows.Where(r =>
                {
                    var stem = selectedRoot.Length > 1 ? selectedRoot[..^1] : selectedRoot;
                    return (r.rootName ?? "").StartsWith(stem, StringComparison.OrdinalIgnoreCase);
                })
                .Select(r => r.categoryName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct();

    private void OnCategoryFilterChanged(ChangeEventArgs e)
    {
        selectedCategory = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<ProductRow> q = allRows;

        if (!ShowArchived)
        {
            q = q.Where(r => r.IsActive && r.VersionIsActive);
        }

        if (!string.IsNullOrWhiteSpace(selectedRoot))
        {
            var stem = selectedRoot.Length > 1 ? selectedRoot[..^1] : selectedRoot;
            q = q.Where(r => (r.rootName ?? "").StartsWith(stem, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(selectedCategory))
            q = q.Where(r => r.categoryName?.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase) == true);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            q = q.Where(r =>
                (r.productName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.productCode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.categoryName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        rows = q.ToList();
    }

    private void ClearFilters()
    {
        selectedRoot = "";
        selectedCategory = "";
        searchText = "";
        filterResetKey++;
        ApplyFilters();
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        searchText = e.Value?.ToString() ?? "";
        ApplyFilters();
    }

    // ===== Preces dialogs =====
    private void OpenDialog()
    {
        isEdit = false;
        createNewVersion = false;

        selectedParentId = null;
        selectedChildId  = null;
        newName = "";
        newCode = "";
        showValidation = false;

        newVersionName = null;
        newVersionRasejums = null;
        newVersionDate = null;
        newVersionComment = null;

        SetMainDialog("Product");
    }

    

    private void OpenEditDialog()
{
    if (selected is null || selectedDetails is null) return;

    isEdit = true;
    createNewVersion = false;
    copyTechnologySteps = false;   // ✅ ŠO TU PIEVIENO

    selectedChildId = categories
        .FirstOrDefault(c => c.CategoryName.Equals(selected.categoryName ?? "", StringComparison.OrdinalIgnoreCase))?.Id;

    selectedParentId = (selectedChildId is int childId)
        ? categories.FirstOrDefault(c => c.Id == childId)?.ParentId
        : null;

    newName = selected.productName ?? "";
    newCode = selected.productCode ?? "";

    newVersionName     = selectedDetails.VersionName ?? "";
    newVersionRasejums = selectedDetails.VersionRasejums ?? "";
    newVersionComment  = selectedDetails.VersionComment ?? "";
    newVersionDate     = selectedDetails.VersionDate;

    showValidation = false;
    SetMainDialog("Product");
}

private void CloseDialog()
{
    SetMainDialog(null);
    isEdit = false;
    createNewVersion = false;
    copyTechnologySteps = false; // ✅ PIEVIENO ŠO
}


    private async Task SaveAsync()
    {
        showValidation = true;

        var isCreate = !isEdit;

        if (isCreate)
        {
            if (selectedParentId is null || selectedChildId is null ||
                string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(newCode))
                return;
        }
        else
        {
            if (selectedChildId is null ||
                string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(newCode))
                return;
        }

        if ((isCreate || createNewVersion) &&
            (string.IsNullOrWhiteSpace(newVersionName) || newVersionDate is null))
            return;

        try
        {
            if (isCreate)
            {
                var payload = new
                {
                    productName = newName,
                    productCode = newCode,
                    categoryId  = selectedChildId!.Value,
                    versionName     = newVersionName,
                    versionRasejums = newVersionRasejums,
                    versionDate     = newVersionDate?.ToString("yyyy-MM-dd"),
                    versionComment  = newVersionComment
                };

                var resp = await Http.PostAsJsonAsync("http://localhost:5270/api/products/create", payload);
                resp.EnsureSuccessStatusCode();
            }
            else
            {
                var payload = new
{
    productId   = selected!.id,
    productName = newName,
    productCode = newCode,
    categoryId  = selectedChildId!.Value,

    versionId = selectedDetails?.VersionId,
    versionName     = newVersionName,
    versionRasejums = newVersionRasejums,
    versionDate     = newVersionDate?.ToString("yyyy-MM-dd"),
    versionComment  = newVersionComment,

    createNewVersion = createNewVersion,
    copyTechnologySteps = createNewVersion && copyTechnologySteps
};


                var resp = await Http.PutAsJsonAsync("http://localhost:5270/api/products/update", payload);
                resp.EnsureSuccessStatusCode();
            }

            allRows = await Http.GetFromJsonAsync<List<ProductRow>>("http://localhost:5270/api/products/list") ?? new();
            ApplyFilters();

            if (selected is not null)
            {
                await OnRowClicked(selected);
            }

            CloseDialog();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Save error: " + ex.Message);
        }
    }

    // ===== Rindu atlase kreisajā gridā =====
    private async Task Grid_RowSelected(RowSelectEventArgs<ProductRow> args)
    {
        if (args?.Data is not null)
            await OnRowClicked(args.Data);
    }

private async Task LoadActiveWorkCentersAsync()
{
    workCenters = (await Http.GetFromJsonAsync<List<WorkCenter>>(
    "http://localhost:5270/api/workcenters") ?? new())
    .ToList();
}

    private async Task DeletePartAsync(int productToPartId)
{
    var yes = await JS.InvokeAsync<bool>("confirm", "Vai tiešām vēlies dzēst šo detaļu?");
    if (!yes) return;

    var resp = await Http.DeleteAsync($"http://localhost:5270/api/products/delete-part/{productToPartId}");
    if (!resp.IsSuccessStatusCode)
    {
        var b = await resp.Content.ReadAsStringAsync();
        await JS.InvokeVoidAsync("alert", $"Dzēšana neizdevās: {b}");
        return;
    }

    // atjauno sarakstu
    selectedPartDetails = await Http.GetFromJsonAsync<List<ProductDetailDto>>(
        $"http://localhost:5270/api/products/details-by-product?id={selected!.id}");
    worksByParts = await Http.GetFromJsonAsync<List<WorksByPartDto>>(
        $"http://localhost:5270/api/products/works-by-product?id={selected!.id}");
    
    StateHasChanged();
}

    private async Task OnRowClicked(ProductRow row)
{
if (row.VersionId == null || row.VersionId == 0)
{
    return;
}

    selected = row;

    openParts.Clear();

    selectedDetails = null;
    selectedPartDetails = null;
    worksByParts = null;

    loadingDetails = true;
    loadingPartDetails = true;
    loadingWorks = true;

    try
    {
        selectedDetails = await Http.GetFromJsonAsync<ProductContentDto>(
            $"http://localhost:5270/api/products/content?versionId={row.VersionId}");

            selectedPartDetails = await Http.GetFromJsonAsync<List<ProductDetailDto>>(
                $"http://localhost:5270/api/products/details-by-version?versionId={row.VersionId}");

            worksByParts = await Http.GetFromJsonAsync<List<WorksByPartDto>>(
                $"http://localhost:5270/api/products/works-by-version?versionId={row.VersionId}");
    }
    finally
    {
        loadingDetails = false;
        loadingPartDetails = false;
        loadingWorks = false;
        StateHasChanged();
    }
}

    private async Task SoftDeleteSelected()
    {
        if (selected is null) return;

        var yes = await JS.InvokeAsync<bool>("confirm",
            $"Vai tiešām vēlies dzēst preci '{selected.productName}'?");

        if (!yes) return;

        try
        {
            var resp = await Http.DeleteAsync($"http://localhost:5270/api/products/delete?id={selected.id}");
            resp.EnsureSuccessStatusCode();

            rows = await Http.GetFromJsonAsync<List<ProductRow>>("http://localhost:5270/api/products/list") ?? new();
            allRows = new List<ProductRow>(rows);

            selected = null;
            selectedDetails = null;
            selectedPartDetails = null;
            worksByParts = null;

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Delete error: " + ex.Message);
        }
    }

    // ===== Soļi (dialogs) =====
    private List<ManaApp.Models.PartStepDto> editingSteps = new();

    private string? stepsError;

    private bool HasInvalidSteps =>
    editingSteps == null || editingSteps.Any(s =>
        string.IsNullOrWhiteSpace(s.StepName)
        || s.StepType <= 0
        || s.WorkCentrId <= 0);

    private int currentStepsPartId;

    private async Task<List<ManaApp.Models.PartStepDto>> LoadStepsAsync(int productToPartId)
    {
        var url = $"http://localhost:5270/api/products/steps-by-part?productToPartId={productToPartId}";
        var steps = await Http.GetFromJsonAsync<List<ManaApp.Models.PartStepDto>>(url) ?? new();
        return steps;
    }

    private async Task OpenStepsForPart(string? topPartCode)
    {       
        stepsError = null;
        
        if (string.IsNullOrWhiteSpace(topPartCode) || selectedPartDetails is null) return;

        var detail = selectedPartDetails.FirstOrDefault(x => x.TopPartCode == topPartCode);
        if (detail is null) return;

        currentStepsPartId = detail.ProductToPartId;
        var steps = await LoadStepsAsync(detail.ProductToPartId);
        editingSteps = new List<PartStepDto>(steps);

// ielādē dropdown avotus katru reizi atverot
await LoadActiveWorkCentersAsync();
await LoadActiveStepTypesAsync();

        SetMainDialog("Steps");

        StateHasChanged();
    }

private async Task CloseStepsDialog()
{
    isStepTypesOpen = false;
    isWorkCentersOpen = false;

    stepsError = null;

    // Pēc soļu rediģēšanas/dzēšanas atsvaidzini labo pusi (“Tehnoloģija” sarakstu)
    if (selected is not null)
    {
        selectedPartDetails = await Http.GetFromJsonAsync<List<ProductDetailDto>>(
            $"http://localhost:5270/api/products/details-by-version?versionId={selected!.VersionId}") ?? new();

        worksByParts = await Http.GetFromJsonAsync<List<WorksByPartDto>>(
            $"http://localhost:5270/api/products/works-by-version?versionId={selected!.VersionId}") ?? new();
    }

    SetMainDialog(null);

    StateHasChanged();
}

    private void AddStep()
{
    if (currentStepsPartId == 0) return;
    editingSteps ??= new();

    var nextOrder = editingSteps.Count > 0
        ? editingSteps.Max(x => x.StepOrder) + 10
        : 10;

    var detail = selectedPartDetails?
        .FirstOrDefault(x => x.ProductToPartId == currentStepsPartId);

    var stage = detail?.Stage ?? 0;

    var defaultStepTypeId = stageStepTypeMap
        .FirstOrDefault(x => x.Stage == stage && x.IsActive)?
        .Step_Type_ID ?? 0;
    
    var defaultWcId = workCenters?.FirstOrDefault()?.Id ?? 0;

    editingSteps.Add(new PartStepDto
    {
        Id = 0,
        ProductToPartId = currentStepsPartId,
        StepOrder = nextOrder,
        StepName = "",
        StepType = defaultStepTypeId,
        WorkCentrId = defaultWcId,
        ParallelGroup = 0,
        IsMandatory = false,
        IsFinal = false,
        Comments = ""
    });

    StateHasChanged();
}

    private void MoveUp(PartStepDto s)
    {
        var idx = editingSteps.IndexOf(s);
        if (idx <= 0) return;
        (editingSteps[idx - 1], editingSteps[idx]) = (editingSteps[idx], editingSteps[idx - 1]);
        RecalcOrders();
    }

    private void MoveDown(PartStepDto s)
    {
        var idx = editingSteps.IndexOf(s);
        if (idx < 0 || idx >= editingSteps.Count - 1) return;
        (editingSteps[idx + 1], editingSteps[idx]) = (editingSteps[idx], editingSteps[idx + 1]);
        RecalcOrders();
    }

private void RecalcOrders()
{
    for (int i = 0; i < editingSteps.Count; i++)
    {
        editingSteps[i].StepOrder = (i + 1) * 10;
    }

    // ❗ NEUZSPIEŽAM Final automātiski
    // Final ir lietotāja atbildība

    StateHasChanged();
}


    private async Task DeleteStepAsync(PartStepDto s)
    {
        var yes = await JS.InvokeAsync<bool>("confirm", $"Dzēst soli “{s.StepName}”?");
        if (!yes) return;

        if (s.Id == 0)
        {
            editingSteps.Remove(s);
            RecalcOrders();
            return;
        }

        var resp = await Http.DeleteAsync($"http://localhost:5270/api/products/step/{s.Id}");
        if (resp.IsSuccessStatusCode)
        {
            editingSteps.Remove(s);
            RecalcOrders();
        }
        else
        {
            var body = await resp.Content.ReadAsStringAsync();
            await JS.InvokeVoidAsync("alert", $"Dzēšana neizdevās: {body}");
        }
    }

    private bool ValidateFinalRule()
    {
        if (editingSteps is null || editingSteps.Count == 0) return false;

        var finals = editingSteps.Where(s => s.IsFinal).ToList();
        if (finals.Count != 1) return false;

        var final = finals[0];
        var maxOrder = editingSteps.Max(s => s.StepOrder);
        return final.StepOrder == maxOrder;
    }

private async Task SaveStepsAsync()
{
    
    stepsError = null;

    try
    {
            await InvokeAsync(StateHasChanged);
            stepsError = null;
            RecalcOrders();

        if (editingSteps == null || editingSteps.Count == 0)
            {
                stepsError = "Nav neviena tehnoloģijas soļa.";
                await InvokeAsync(StateHasChanged);
                return;
            }
        await InvokeAsync(StateHasChanged);

        var invalid = editingSteps
    .Select((s, i) => new { s, i })
    .FirstOrDefault(x => string.IsNullOrWhiteSpace(x.s.StepName)
                      || x.s.WorkCentrId <= 0);

    if (invalid != null)
    {
        var field = string.IsNullOrWhiteSpace(invalid.s.StepName)
            ? "nosaukums"
            : "darba centrs";

        await JS.InvokeVoidAsync("alert", $"Izlabo soli #{invalid.i + 1}: nav aizpildīts {field}.");
        return;
    }

if (!ValidateFinalRule())
{
    await JS.InvokeVoidAsync("alert", "Pēdējam solim jābūt atzīmētam kā Final.");
    return;
}

        // 2️⃣ PUT all (pareizais order)
        foreach (var step in editingSteps.Where(s => s.Id > 0))
        {
            var payload = new
            {
                Id = step.Id,
                StepOrder = step.StepOrder,
                StepName = step.StepName,
                StepType = step.StepType,
                WorkCentrId = step.WorkCentrId,
                EstimatedMinutes = step.EstimatedMinutes,
                ParallelGroup = step.IsParallel ? 1 : 0,
                IsMandatory = step.IsMandatory,
                IsFinal = step.IsFinal,
                Comments = step.Comments
            };


            var resp = await Http.PutAsJsonAsync("http://localhost:5270/api/products/step", payload);

                if (!resp.IsSuccessStatusCode)
                {
                    stepsError = await resp.Content.ReadAsStringAsync();
                    await InvokeAsync(StateHasChanged);
                    return;
                }
        }
    // 1️⃣ POST new
        foreach (var step in editingSteps.Where(s => s.Id == 0))
        {
            var payload = new
            {
                ProductToPartId = step.ProductToPartId,
                StepOrder = step.StepOrder,
                StepName = step.StepName,
                StepType = step.StepType,
                WorkCentrId = step.WorkCentrId,
                EstimatedMinutes = step.EstimatedMinutes,
                ParallelGroup = step.IsParallel ? 1 : 0,
                IsMandatory = step.IsMandatory,
                IsFinal = step.IsFinal,
                Comments = step.Comments
            };

var resp = await Http.PostAsJsonAsync("http://localhost:5270/api/products/step", payload);

if (!resp.IsSuccessStatusCode)
{
    stepsError = await resp.Content.ReadAsStringAsync();
    await InvokeAsync(StateHasChanged);
    return;
}

var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
if (result.TryGetProperty("Id", out var idProp))
{
    step.Id = idProp.GetInt32();
}
        }
            SetMainDialog(null);
            await OnRowClicked(selected!);
    }

catch (Exception ex)
{
    await JS.InvokeVoidAsync("alert", ex.Message);
}

}
    // ===== Jaunas detaļas dialogs =====
   private NewPartVm newPart = new();
    private List<PartStepDto> newPartSteps = new();

    private void CloseNewPartDialog() => SetMainDialog(null);

private async Task SaveNewPart()
{
    if (selectedDetails is null || newPart is null || newPart.TopPartId is null) return;

    
 var payload = new
{
    productId     = selected!.id,              // ← API gaida productId
    topPartId     = newPart.TopPartId!.Value,  // ← API gaida topPartId
    qtyPerProduct = newPart.QtyPerProduct      // ← API gaida qtyPerProduct (>=1)
};

    var resp = await Http.PostAsJsonAsync("http://localhost:5270/api/products/add-part", payload);
    if (!resp.IsSuccessStatusCode)
    {
        var b = await resp.Content.ReadAsStringAsync();
        await JS.InvokeVoidAsync("alert", $"Neizdevās saglabāt detaļu: {b}");
        return;
    }

    // atjauno sarakstus (gan detaļas, gan “Tehnoloģiju”)
selectedPartDetails = await Http.GetFromJsonAsync<List<ProductDetailDto>>(
    $"http://localhost:5270/api/products/details-by-product?id={selected!.id}") ?? new();

worksByParts = await Http.GetFromJsonAsync<List<WorksByPartDto>>(
    $"http://localhost:5270/api/products/works-by-product?id={selected!.id}") ?? new();

SetMainDialog(null);

}

private List<TopPartDto> topParts = new();

private async Task DeletePartByCode(string? topPartCode)
{
    if (string.IsNullOrWhiteSpace(topPartCode))
        return;

    var detail = selectedPartDetails?.FirstOrDefault(x => x.TopPartCode == topPartCode);
    if (detail is null)
    {
        await JS.InvokeVoidAsync("alert", "Neatradu šai detaļai ProductToPartId.");
        return;
    }

    await DeletePartAsync(detail.ProductToPartId);
}

private List<StepTypeDto> stepTypes = new();

// pārvaldības dialogs
private string newStepTypeName = "";

private List<StepTypeRow> manageStepTypes = new();

private int? editStepTypeId = null;
private string editStepTypeName = "";

private async Task OpenStepTypeDialogAsync()
{
    // dropdowns (aktīvie)
    stepTypes = await Http.GetFromJsonAsync<List<StepTypeDto>>("http://localhost:5270/api/steptypes") ?? new();

    // pārvaldībai – ielādējam un atfiltrējam tikai aktīvos
    var manage = await Http.GetFromJsonAsync<List<StepTypeDto>>("http://localhost:5270/api/steptypes/manage") ?? new();
    manageStepTypes = manage
        .Where(x => x.IsActive)
        .OrderBy(x => x.StepTypeName)
        .Select(x => new StepTypeRow { Id = x.Id, Name = x.StepTypeName, IsActive = x.IsActive })
        .ToList();

    // notīrām jebkuru iepriekšējo rediģēšanas stāvokli
    editStepTypeId = null;
    editStepTypeName = "";
    isWorkCentersOpen = false;
    isStepTypesOpen = true;
    StateHasChanged();
}

private async Task RefreshStepTypesAsync()
{
    // dropdownam
    stepTypes = await Http.GetFromJsonAsync<List<StepTypeDto>>("http://localhost:5270/api/steptypes") ?? new();

    // pārvaldībai (tikai aktīvie)
    var manage = await Http.GetFromJsonAsync<List<StepTypeDto>>("http://localhost:5270/api/steptypes/manage") ?? new();
    manageStepTypes = manage
        .Where(x => x.IsActive)
        .OrderBy(x => x.StepTypeName)
        .Select(x => new StepTypeRow { Id = x.Id, Name = x.StepTypeName, IsActive = x.IsActive })
        .ToList();
}


// CRUD darbības pārvaldības dialogā
private async Task AddStepTypeAsync()
{
    var name = (newStepTypeName ?? "").Trim();
    if (string.IsNullOrWhiteSpace(name)) { await JS.InvokeVoidAsync("alert","Ievadi nosaukumu."); return; }
    var resp = await Http.PostAsJsonAsync("http://localhost:5270/api/steptypes", new { Name = name });
    if (!resp.IsSuccessStatusCode) { await JS.InvokeVoidAsync("alert", await resp.Content.ReadAsStringAsync()); return; }
    newStepTypeName = "";
    await OpenStepTypeDialogAsync(); // refrešs
    // refrešo dropdown datus
    stepTypes = await Http.GetFromJsonAsync<List<StepTypeDto>>("http://localhost:5270/api/steptypes") ?? new();
    StateHasChanged();
}

private void BeginEditStepType(StepTypeRow row)
{
    editStepTypeId = row.Id;
    editStepTypeName = row.Name;
}

private void CancelEditStepType()
{
    editStepTypeId = null;
    editStepTypeName = "";
}

private async Task SaveEditStepTypeAsync()
{
    if (editStepTypeId is null) return;
    var name = (editStepTypeName ?? "").Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        await JS.InvokeVoidAsync("alert", "Ievadi nosaukumu.");
        return;
    }

    var resp = await Http.PutAsJsonAsync("http://localhost:5270/api/steptypes",
        new { Id = editStepTypeId.Value, Name = name });

    if (!resp.IsSuccessStatusCode)
    {
        await JS.InvokeVoidAsync("alert", await resp.Content.ReadAsStringAsync());
        return;
    }

    editStepTypeId = null;
    editStepTypeName = "";
    await RefreshStepTypesAsync(); // pārlādē gan pārvaldības sarakstu, gan dropdown
}



private async Task DeleteStepTypeAsync(int id)
{
    var ok = await JS.InvokeAsync<bool>("confirm", "Dzēst soļa tipu? (soft delete)");
    if (!ok) return;
    var resp = await Http.DeleteAsync($"http://localhost:5270/api/steptypes/{id}");
    if (!resp.IsSuccessStatusCode) { await JS.InvokeVoidAsync("alert", await resp.Content.ReadAsStringAsync()); return; }
    await OpenStepTypeDialogAsync();
    stepTypes = await Http.GetFromJsonAsync<List<StepTypeDto>>("http://localhost:5270/api/steptypes") ?? new();
    StateHasChanged();
}

// dropdown atsvaidzināšanai

private List<WorkCenterManageDto> manageWorkCenters = new();

private async Task OpenWorkCenterDialogAsync()
{
    newWorkCenterName = "";   
    newWorkCenterCode = "";

    manageWorkCenters = (await Http.GetFromJsonAsync<List<WorkCenterManageDto>>(
        "http://localhost:5270/api/workcenters/manage") ?? new())
        .Where(x => x.IsActive)
        .ToList();

    isStepTypesOpen = false;
    isWorkCentersOpen = true;
}

private string newWorkCenterName = "";
private int? editWorkCenterId = null;
private string editWorkCenterName = "";

private async Task RefreshWorkCentersAsync()
{
    manageWorkCenters = (await Http.GetFromJsonAsync<List<WorkCenterManageDto>>(
    "http://localhost:5270/api/workcenters/manage") ?? new())
    .Where(x => x.IsActive)
    .OrderBy(x => x.WorkCenter_Order)
    .ToList();

}

private async Task AddWorkCenterAsync()
{
    var name = (newWorkCenterName ?? "").Trim();
    var codeInput = (newWorkCenterCode ?? "").Trim().ToUpper();

    if (string.IsNullOrWhiteSpace(name))
    {
        await JS.InvokeVoidAsync("alert", "Ievadi nosaukumu.");
        return;
    }

    if (string.IsNullOrWhiteSpace(codeInput))
        {
            await JS.InvokeVoidAsync("alert", "Ievadi kodu.");
            return;
        }

        if (codeInput.Length != 3)
        {
            await JS.InvokeVoidAsync("alert", "Kodam jābūt tieši 3 simboliem.");
            return;
        }

    var resp = await Http.PostAsJsonAsync(
        "http://localhost:5270/api/workcenters/add",
        new { WorkCentr_Name = name, WorkCentr_Code = codeInput });

    if (!resp.IsSuccessStatusCode)
    {
        await JS.InvokeVoidAsync("alert", await resp.Content.ReadAsStringAsync());
        return;
    }

    newWorkCenterName = "";
    newWorkCenterCode = "";

    await RefreshWorkCentersAsync();
    await LoadActiveWorkCentersAsync();
}

private void CancelEditWorkCenter()
{
    editWorkCenterId = null; editWorkCenterName = "";
}

private async Task SaveEditWorkCenterAsync()
{
      if (editWorkCenterId is null) return;
    
    var name = (editWorkCenterName ?? "").Trim();
    if (string.IsNullOrWhiteSpace(name)) { await JS.InvokeVoidAsync("alert","Ievadi nosaukumu."); return; }

    // ģenerējam kodu no nosaukuma (bez atstarpēm, lielie burti)
var code = (name ?? "").Trim().ToUpper().Replace(" ", "_");

var row = manageWorkCenters.First(x => x.Id == editWorkCenterId!.Value);

var resp = await Http.PutAsJsonAsync("http://localhost:5270/api/workcenters/update",
    new 
    { 
        Id = row.Id,
        WorkCentr_Name = name,
        WorkCentr_Code = code,
        WorkCenter_Order = row.WorkCenter_Order,
        Step_Type_ID = row.Step_Type_ID
    });


    if (!resp.IsSuccessStatusCode)
    {
        await JS.InvokeVoidAsync("alert", await resp.Content.ReadAsStringAsync());
        return;
    }
    editWorkCenterId = null; editWorkCenterName = "";
    await RefreshWorkCentersAsync();
    await LoadActiveWorkCentersAsync();
    StateHasChanged();

}

private async Task DeleteWorkCenterAsync(int id)
{
    var ok = await JS.InvokeAsync<bool>("confirm", "Dzēst darba centru? (soft delete)");
    if (!ok) return;

    var resp = await Http.DeleteAsync($"http://localhost:5270/api/workcenters/{id}");
    if (!resp.IsSuccessStatusCode)
    {
        await JS.InvokeVoidAsync("alert", await resp.Content.ReadAsStringAsync());
        return;
    }
    await RefreshWorkCentersAsync();
    await LoadActiveWorkCentersAsync();

}

private bool NewPartExistsAlready
{
    get
    {
        if (newPart?.TopPartId is not int id) return false;
        var sel = topParts.FirstOrDefault(t => t.Id == id);
        return sel != null && (selectedPartDetails?.Any(d => d.TopPartCode == sel.TopPartCode) ?? false);
    }
}

private bool NewPartInvalid =>
    newPart is null
    || newPart.TopPartId is null
    || newPart.QtyPerProduct < 1
    || NewPartExistsAlready;

private void SetRootFilter(string value)
{
    selectedRoot = value ?? "";
    ApplyFilters();
    StateHasChanged();
}

private bool HasAnyFilters =>
    !string.IsNullOrWhiteSpace(selectedRoot)
    || !string.IsNullOrWhiteSpace(selectedCategory)
    || !string.IsNullOrWhiteSpace(searchText);

private bool IsLastStep(PartStepDto s)
{
    if (editingSteps is null || editingSteps.Count == 0)
        return false;

    var maxOrder = editingSteps.Max(x => x.StepOrder);
    return s.StepOrder == maxOrder;
}

private void SetFinalStep(PartStepDto step)
{
    if (!IsLastStep(step))
        return;

    foreach (var s in editingSteps)
        s.IsFinal = false;

    step.IsFinal = true;

    StateHasChanged();
}

private async Task LoadActiveStepTypesAsync()
{
    stepTypes = await Http.GetFromJsonAsync<List<StepTypeDto>>(
        "http://localhost:5270/api/steptypes") ?? new();
}
private bool copyTechnologySteps = false;

private void SetCopyTechnologySteps(bool value)
{
    // Kopēt drīkst tikai tad, ja veido jaunu versiju
    copyTechnologySteps = createNewVersion && value;
}

private async Task OpenNewPartDialog()
{
    newPart = new NewPartVm { TopPartId = null, QtyPerProduct = 1 };
    newPartSteps = new List<ManaApp.Models.PartStepDto>();
    SetMainDialog("NewPart");
}

private Task HandleBeginEditStepType(StepTypeRow row)
{
    editStepTypeId = row.Id;
    editStepTypeName = row.Name;
    return Task.CompletedTask;
}

private void BeginEditWorkCenter(WorkCenterManageDto it)
{
    editWorkCenterId = it.Id;
    editWorkCenterName = it.WorkCentr_Name;
}

private async Task HandleNewPartSave(NewPartVm model)
{
    await SaveNewPart();
}

private bool IsProductDialogOpen => activeMainDialog == "Product";
private void SetMainDialog(string? dialog)
{
    activeMainDialog = dialog;
}

private class AddPartResponse
{
    public int Id { get; set; }
}
private async Task ToggleTopPartAsync(TopPartDto part)
{
    if (selected is null)
        return;

    selectedPartDetails ??= new List<ProductDetailDto>();

var existing = selectedPartDetails
    .FirstOrDefault(x => x.TopPartId == part.Id);

    if (existing is null)
    {
        // ===== ADD =====
        var resp = await Http.PostAsJsonAsync(
            "http://localhost:5270/api/products/add-part",
            new
            {
                productId = selected.id,
                topPartId = part.Id,
                qtyPerProduct = 1
            });

        if (!resp.IsSuccessStatusCode)
            {
                var msg = await resp.Content.ReadAsStringAsync();

                if (msg.Contains("jau ir pievienota"))
                {
                    await JS.InvokeVoidAsync("alert", "Detaļa jau ir šai versijai.");
                    return;
                }

                await JS.InvokeVoidAsync("alert", msg);
                return;
            }

        var result = await resp.Content.ReadFromJsonAsync<AddPartResponse>();

        if (result is null)
        {
            await JS.InvokeVoidAsync("alert", "Serveris neatgrieza ID.");
            return;
        }

        // 👇 pievienojam lokāli bez reload
        selectedPartDetails.Add(new ProductDetailDto
        {
            ProductToPartId = result.Id,
            TopPartId = part.Id,
            TopPartName = part.TopPartName,
            TopPartCode = part.TopPartCode,
            Quantity = 1
        });
    }
    else
    {
        // ===== REMOVE =====
        var resp = await Http.PutAsJsonAsync(
            "http://localhost:5270/api/products/toggle-part",
            new
            {
                ProductToPartId = existing.ProductToPartId,
                IsActive = false
            });

        if (!resp.IsSuccessStatusCode)
        {
            await JS.InvokeVoidAsync("alert", await resp.Content.ReadAsStringAsync());
            return;
        }

        // 👇 izņemam lokāli bez reload
        selectedPartDetails.Remove(existing);
    }

    StateHasChanged();
}

private bool _showArchived;

private bool ShowArchived
{
    get => _showArchived;
    set
    {
        if (_showArchived == value) return;

        _showArchived = value;
        ApplyFilters();
    }
}

private void OnArchivedChanged(ChangeEventArgs e)
{
    ApplyFilters();
}

async Task MoveUp(WorkCenterManageDto wc)
{

    var list = manageWorkCenters.OrderBy(x => x.WorkCenter_Order).ToList();
    var index = list.FindIndex(x => x.Id == wc.Id);
    
    if (index <= 0)
        return;

    var above = list[index - 1];

    var current = list[index];
        current.WorkCenter_Order -= 10;
        above.WorkCenter_Order += 10;

    await Http.PutAsJsonAsync("http://localhost:5270/api/workcenters/update", current);
    await Http.PutAsJsonAsync("http://localhost:5270/api/workcenters/update", above);

    await RefreshWorkCentersAsync();
    StateHasChanged();
}

async Task MoveDown(WorkCenterManageDto wc)
{
    
    var list = manageWorkCenters.OrderBy(x => x.WorkCenter_Order).ToList();
    

    var index = list.FindIndex(x => x.Id == wc.Id);
    
    if (index == -1 || index >= list.Count - 1)
        return;

    var below = list[index + 1];

    var current = list[index];
        current.WorkCenter_Order += 10;
        below.WorkCenter_Order -= 10;;

    await Http.PutAsJsonAsync("http://localhost:5270/api/workcenters/update", current);
    await Http.PutAsJsonAsync("http://localhost:5270/api/workcenters/update", below);

    await RefreshWorkCentersAsync();
    StateHasChanged();
}

}

6) Proukta Tehnoloģijas box - pievieno konkrētās versijas Tehnoloģijas aprakstu - kuras detaļas;
atzīmē ar check box - pārvērš Db neaktīvu; 

@namespace ManaApp.Components.Products
@using ManaApp.Models

<div class="technology-panel @(IsReadOnly ? "readonly-panel" : "")">

    <div class="technology-panel-header">
        Tehnoloģija
    </div>

    <div class="technology-panel-body">

    @if (Selected is not null)
    {
        <div class="technology-header-actions">
            @if (OnAddPart.HasDelegate)
            {
                <button type="button"
                        class="action-btn tech-add-btn"
                        title="Pievienot jaunu detaļu"
                        @onclick="OnAddPart">
                    Detaļas
                </button>

                <button type="button"
                        class="action-btn tech-manage-btn"
                        title="Pārvaldīt soļu tipus"
                        @onclick="OnOpenStepTypes">
                    Soļu tipi
                </button>

                <button type="button"
                        class="action-btn tech-manage-btn"
                        title="Pārvaldīt darba centrus"
                        @onclick="OnOpenWorkCenters">
                    Darba centri
                </button>
            }
        </div>
    }

        @if (Selected is null)
        {
            <div style="opacity:.7">Izvēlies rindu kreisajā pusē.</div>
        }
        else if (LoadingPartDetails || LoadingWorks)
        {
            <div style="opacity:.7">Ielādēju…</div>
        }
else if (AllTopParts is null || AllTopParts.Count == 0)
{
    <div style="opacity:.7">Nav detaļu.</div>
}
else
{
    <table class="tech-table">
        <thead>
            <tr>
                <th></th>
                <th>Kods</th>
                <th>Nosaukums</th>
                <th>Skaits</th>
                <th>+ soļi</th>
            </tr>
        </thead>
            <tbody>

                    @foreach (var group in (AllTopParts ?? new List<TopPartDto>())
                                            .OrderBy(x => x.Stage)
                                            .GroupBy(x => x.Stage))
                    {
                        <tr class="tech-stage-row">
                            <td colspan="5">
                                <strong>
                                    @(group.Key == 1 ? "DETAIL"
                                    : group.Key == 2 ? "ASSEMBLY"
                                    : "FINISHING")
                                </strong>
                            </td>
                        </tr>

@foreach (var part in group)
                        {
                            <tr>
                                <td>
                                    <input type="checkbox"
                                        checked="@IsPartSelected(part)"
                                        @onchange="e => OnTogglePart(part, e.Value)" />
                                </td>
                                <td>@part.TopPartCode</td>
                                <td>@part.TopPartName</td>
                                <td>
                                    <input type="number"
                                        min="1"
                                        value="@GetQty(part)"
                                        disabled="@(!IsPartSelected(part))" />
                                </td>
                                <td>
                                    <button type="button"
                                        class="action-btn tech-step-btn"
                                        title="Pievienot darba soļus"
                                        disabled="@(!IsPartSelected(part))"
                                        @onclick="() => OnOpenSteps.InvokeAsync(part.TopPartCode)">
                                        +
                                    </button>
                                </td>
                            </tr>

                    var workItem = WorksByParts?
                        .FirstOrDefault(w => w.TopPartCode == part.TopPartCode);

                    @if (IsPartSelected(part) || (workItem?.Steps?.Any() == true))
                    {
                        <tr class="tech-steps-row">
                            <td></td>
                            <td colspan="4">

                                @if (workItem?.Steps != null && workItem.Steps.Any())
                                {
                                    <div class="steps-container">
@foreach (var stepWithIndex in workItem.Steps
                                       .OrderBy(s => s.StepOrder)
                                       .Select((step, index) => new { step, index }))
            {
                <div class="step-row">
                    <span class="step-order">
                        @(stepWithIndex.index + 1).
                    </span>

                    <span class="step-name">
                        @stepWithIndex.step.StepName
                    </span>

                    <span class="step-wc">
                        @stepWithIndex.step.WorkCenter
                    </span>
                </div>
            }
        </div>
                                }
                                else
                                {
                                    <div class="no-steps">Nav tehnoloģijas soļu</div>
                                }

                            </td>
                        </tr>
                    }
                        }
                    }

            </tbody>
    </table>
}

    </div>
</div>

@code {
    [Parameter] public ProductRow? Selected { get; set; }
    [Parameter] public bool LoadingPartDetails { get; set; }
    [Parameter] public bool LoadingWorks { get; set; }
    [Parameter] public List<ProductDetailDto>? SelectedPartDetails { get; set; }
    [Parameter] public List<WorksByPartDto>? WorksByParts { get; set; }
    [Parameter] public List<TopPartDto>? AllTopParts { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback OnAddPart { get; set; }
    [Parameter] public EventCallback<string?> OnOpenSteps { get; set; }
    [Parameter] public EventCallback<string?> OnDeletePart { get; set; }
    [Parameter] public EventCallback OnOpenStepTypes { get; set; }
    [Parameter] public EventCallback OnOpenWorkCenters { get; set; }
    [Parameter] public EventCallback<TopPartDto> OnToggleTopPart { get; set; }
    [Parameter] public bool IsReadOnly { get; set; }
    private HashSet<string> openParts = new();

private void TogglePart(string? code)
{
    if (string.IsNullOrWhiteSpace(code)) return;

    if (!openParts.Add(code))
        openParts.Remove(code);
}

private bool IsPartSelected(TopPartDto part)
{
    return SelectedPartDetails?.Any(x => x.TopPartCode == part.TopPartCode) == true;
}

private async Task OnTogglePart(TopPartDto part, object? value)
{
    var isChecked = value is bool b && b;

if (OnToggleTopPart.HasDelegate)
{
    await OnToggleTopPart.InvokeAsync(part);
}
}

private int GetQty(TopPartDto part)
{
    return SelectedPartDetails?
        .FirstOrDefault(x => x.TopPartCode == part.TopPartCode)?
        .Quantity ?? 1;
}

}

7)  ProductStepDialog - aktīvā topproduct secīgie step soļi - izveido konkrētus soļus;
obligāti pēdējam ir jābūt IsFinal=True - lai zinātu, ka detaļa ir gatava no detail posma

@using ManaApp.Models
@using System.Linq
@using Syncfusion.Blazor.Grids
@using Syncfusion.Blazor.Popups
@using Syncfusion.Blazor.DropDowns
@using Syncfusion.Blazor.Inputs
@inject IJSRuntime JS


@if (Visible)
{
<div class="steps-dialog-modal">
<div class="steps-dialog-window">

 <div class="steps-dialog-header">
    Darba soļu secība
</div>         

<div class="steps-dialog">


    @if (Steps is null || Steps.Count == 0)
    {
        <div style="opacity:.7">Šai detaļai soļu nav.</div>
    }
    else
    {
    <div class="steps-table">
    <div class="steps-table-head steps-grid">
        <div class="steps-th steps-th-order">Secība</div>
        <div class="steps-th">Nosaukums</div>
        <div class="steps-th steps-th-workcenter">Darba centrs</div>
        <div class="steps-th steps-th-flags">Statuss</div>
        <div class="steps-th steps-th-time">Min</div>
        <div class="steps-th">Komentārs</div>
        <div class="steps-th steps-th-actions"></div>
    </div>

<div class="steps-table-body"
     @onmouseup="EndDrag"
     @onmouseleave="EndDrag">
    @if (Steps == null)
    {
        <div></div>
    }
            @for (int i = 0; i < (Steps?.Count ?? 0); i++)
            {
                var s = Steps![i];
                var index = i;

<div 
     @key="s"
     class="steps-tr steps-grid @( _selectedIndex == index ? "dragging" : "" )"
     @onmouseenter="() => OnDragEnter(index)">
              
                <div class="steps-order-cell">
                <span class="steps-drag-handle"
                    @onmousedown:preventDefault="true"
                    @onmousedown="() => StartDrag(index)"
                    title="Pārvietot">⋮⋮</span>
                </div>

                <div>
                    <input class="steps-input"
                        type="text"
                        @bind="s.StepName"
                        @bind:event="oninput"
                        placeholder="Pievienot nosaukumu" />
                </div>

                <div>
                    <SfDropDownList TValue="int" TItem="WorkCenter"
                                    CssClass="steps-ddl"
                                    PopupCssClass="steps-ddl-popup-small"
                                    DataSource="WorkCenters"
                                    Value="s.WorkCentrId"
                                    ValueChanged="@(v => s.WorkCentrId = v)"
                                    Width="100%">
                        <DropDownListFieldSettings Text="WorkCentr_Name" Value="Id" />
                    </SfDropDownList>
                </div>

                <div class="steps-flags-cell">
                    <label class="steps-flag" title="Paralēls solis">
                        <input type="checkbox" @bind="s.IsParallel" />
                        <span class="flag-icon parallel"></span>
                    </label>

                    <label class="steps-flag" title="Pēdējais (Final) solis">
                        <input type="checkbox"
                            checked="@s.IsFinal"
                            disabled="@( !IsLastStep(s) )"
                            @onclick="() => OnSetFinal.InvokeAsync(s)" />
                        <span class="flag-icon final"></span>
                    </label>
                </div>

                <div>
                    <input class="steps-input"
                        type="number"
                        min="0"
                        style="width:70px"
                        @bind="s.EstimatedMinutes" />
                </div>

                <div>
                    <input class="steps-input steps-input-pill"
                        type="text"
                        @bind="s.Comments"
                        @bind:event="oninput" />
                </div>

                <div class="steps-td-right">
                    <button type="button"
                            class="steps-btn"
                            @onclick="() => OnDelete.InvokeAsync(s)"
                            title="Dzēst">🗑</button>
                </div>
            </div>
        }
                        
    </div>
</div>
    }
</div>


<div class="steps-footer-wrapper">

    <div class="steps-footer">

        <button type="button"
                class="steps-btn-add"
                title="Pievieno jaunu tehnoloģijas soli šai detaļai"
                @onclick="OnAddStep">
            + Pievienot soli
        </button>

<button type="button"
        class="steps-btn-save"
        title="Saglabāt"
        @onclick="HandleSave">
    Saglabāt
</button>

        <button type="button"
                class="steps-btn-close"
                title="Aizvērt logu"
                @onclick="OnClose">
            Aizvērt
        </button>

    </div>
</div>

</div>
</div>
}


@code {
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public List<PartStepDto>? Steps { get; set; }
    [Parameter] public string? StepsError { get; set; }
    [Parameter] public IEnumerable<StepTypeDto>? StepTypes { get; set; }
    [Parameter] public IEnumerable<WorkCenter>? WorkCenters { get; set; }
    [Parameter] public EventCallback<PartStepDto> OnMoveUp { get; set; }
    [Parameter] public EventCallback<PartStepDto> OnMoveDown { get; set; }
    [Parameter] public EventCallback<PartStepDto> OnDelete { get; set; }
    [Parameter] public EventCallback<PartStepDto> OnSetFinal { get; set; }

private bool IsLastStep(PartStepDto s)
    {
        if (Steps is null || Steps.Count == 0)
            return false;

        var maxOrder = Steps.Max(x => x.StepOrder);
        return s.StepOrder == maxOrder;
    }
[Parameter] public EventCallback OnOpenStepTypes { get; set; }
[Parameter] public EventCallback OnOpenWorkCenters { get; set; }

[Parameter] public EventCallback OnAddStep { get; set; }
[Parameter] public EventCallback OnSave { get; set; }
[Parameter] public EventCallback OnClose { get; set; }

private int? _dragIndex = null;

private int? _selectedIndex = null;

private void SelectForMove(int index)
{   
        if (_selectedIndex is null)
        {
            _selectedIndex = index;
            StateHasChanged();
            return;
        }

        if (_selectedIndex == index)
        {
            _selectedIndex = null;
            StateHasChanged();
            return;
        }

    var item = Steps![_selectedIndex.Value];
    Steps.RemoveAt(_selectedIndex.Value);
    Steps.Insert(index, item);

    _selectedIndex = null;

    for (int i = 0; i < Steps.Count; i++)
        Steps[i].StepOrder = (i + 1) * 10;

    StateHasChanged();
}
private int? _hoverIndex = null;

private void SetHover(int index)
{
    if (_selectedIndex != null)
        _hoverIndex = index;
}

private bool _isDragging = false;

private void StartDrag(int index)
{
    _selectedIndex = index;
    _hoverIndex = index;
    _isDragging = true;
    StateHasChanged();
}

private void OnDragEnter(int index)
{
    if (!_isDragging || _selectedIndex == null || Steps == null)
        return;

    if (index == _selectedIndex)
        return;

    var item = Steps[_selectedIndex.Value];
    Steps.RemoveAt(_selectedIndex.Value);
    Steps.Insert(index, item);

    _selectedIndex = index;
    _hoverIndex = index;

    for (int i = 0; i < Steps.Count; i++)
        Steps[i].StepOrder = (i + 1) * 10;

    StateHasChanged();
}

private void EndDrag()
{
    _isDragging = false;
    _selectedIndex = null;
    _hoverIndex = null;

    if (Steps != null)
    {
        for (int i = 0; i < Steps.Count; i++)
            Steps[i].StepOrder = (i + 1) * 10;
    }

    StateHasChanged();
}

private void OnDialogOpened(Syncfusion.Blazor.Popups.OpenEventArgs args)
{
    args.PreventFocus = true;
}

private async Task HandleSave()
{
    await OnSave.InvokeAsync();
    StateHasChanged();
}

}
