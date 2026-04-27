// TopPartsController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopPartsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public TopPartsController(AppDbContext db) => _db = db;

        // GET: api/topparts
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _db.TopParts
                .Where(x => x.IsActive)
                .OrderBy(x => x.Stage)
                .ThenBy(x => x.TopPartName)
                .ToListAsync();

            return Ok(rows);
        }

        // POST: api/topparts
[HttpPost]
public async Task<IActionResult> Create([FromBody] TopPart dto)
{
    if (string.IsNullOrWhiteSpace(dto.TopPartName))
        return BadRequest("Nosaukums ir obligāts.");

    if (string.IsNullOrWhiteSpace(dto.TopPartCode) || dto.TopPartCode.Length != 3)
        return BadRequest("Kods obligāts un jābūt tieši 3 simboliem.");

    var exists = await _db.TopParts
        .AnyAsync(x => x.TopPartCode == dto.TopPartCode);

    if (exists)
        return Conflict("Šāds kods jau eksistē.");

    dto.TopPartName = dto.TopPartName.Trim();
    dto.TopPartCode = dto.TopPartCode.Trim().ToUpper();
    dto.IsActive = true;

    _db.TopParts.Add(dto);
    await _db.SaveChangesAsync();

    return Ok(dto);
}

        // PUT: api/topparts
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] TopPart dto)
        {
            var row = await _db.TopParts
                .FirstOrDefaultAsync(x => x.Id == dto.Id && x.IsActive);

            if (row is null)
                return NotFound();

            if (string.IsNullOrWhiteSpace(dto.TopPartName))
                return BadRequest("Nosaukums ir obligāts.");

            row.TopPartName = dto.TopPartName;

            await _db.SaveChangesAsync();
            return Ok(row);
        }

        // DELETE: api/topparts/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _db.TopParts
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

            if (row is null)
                return NotFound();

            row.IsActive = false;
            await _db.SaveChangesAsync();

            return Ok();
        }

        // GET: api/topparts/by-version?versionId=5
[HttpGet("by-version")]
public async Task<IActionResult> GetByVersion(int versionId)
{
    var rows = await (
        from ptp in _db.ProductTopParts
        join tp in _db.TopParts on ptp.TopPartId equals tp.Id
        where ptp.VersionId == versionId 
            && ptp.IsActive
            && tp.Stage == 1
        select new
        {
            Id = ptp.Id,
            TopPart_Id = tp.Id,
            TopPart_Name = tp.TopPartName
        }
    )
    .OrderBy(x => x.TopPart_Name)
    .ToListAsync();

    return Ok(rows);
}

[HttpGet("stock-from-movements")]
public async Task<IActionResult> GetStockFromMovements([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var rows = await (
        from ptp in _db.ProductTopParts
        join tp in _db.TopParts on ptp.TopPartId equals tp.Id
        where ptp.VersionId == versionId
              && ptp.IsActive
              && tp.IsActive
              && tp.Stage == 1
        select new
        {
            TopPartId = tp.Id,
            TopPartName = tp.TopPartName,

            StockQty =
            _db.StockMovements
                .Where(sm =>
                    sm.IsActive &&
                    sm.Version_ID == ptp.VersionId &&
                    sm.BatchProduct_ID == ptp.Id)
                .Sum(sm => (int?)sm.Stock_Qty) ?? 0
        }
    ).ToListAsync();

    return Ok(rows);
}

[HttpGet("planned-parts")]
public async Task<IActionResult> GetPlannedParts([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

            var data = await (
                from bp in _db.BatchProducts

                join ptp in _db.ProductTopParts
                    on bp.ProductToPart_ID equals ptp.Id into ptpGroup
                from ptp in ptpGroup.DefaultIfEmpty()

                join tp in _db.TopParts
                    on ptp.TopPartId equals tp.Id into tpGroup
                from tp in tpGroup.DefaultIfEmpty()

                where bp.Version_Id == versionId
                    && bp.IsActive
                    && ptp != null   // ✅ TIKAI detaļas

                group bp by new 
                { 
                    bp.Version_Id, 
                    TopPartId = tp != null ? tp.Id : bp.ProductToPart_ID 
                } into g

                select new
                {
                    VersionId = g.Key.Version_Id,
                    TopPartId = g.Key.TopPartId,
                    PlannedQty = g.Sum(x => x.Planned_Qty)
                }
            ).ToListAsync();

    return Ok(data);
}

[HttpGet("planned-correct")]
public async Task<IActionResult> GetPlannedCorrect([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

var data = await (
    from bp in _db.BatchProducts

    join ptp in _db.ProductTopParts
        on bp.ProductToPart_ID equals ptp.Id

    where bp.IsActive
      && bp.Version_Id == versionId
      && ptp.IsActive
      && bp.ProductToPart_ID != null

        && !_db.StockMovements
            .Any(sm =>
                sm.IsActive &&
                sm.BatchProduct_ID == bp.ID &&
                sm.Move_Type != MoveType.PLANNED)

          // tikai DETAIL tasks = 5
          && !_db.Tasks
                .Join(_db.TopPartSteps,
                    t => t.TopPartStep_ID,
                    ts => ts.Id,
                    (t, ts) => new { t, ts })
                .Any(x =>
                    x.t.BatchProduct_ID == bp.ID &&
                    x.t.IsActive &&
                    x.ts.StepType == 1 &&
                    x.t.Tasks_Status != 5)

    group bp by new
    {
        bp.Version_Id,
        ptp.TopPartId
    } into g

    select new
    {
        VersionId = g.Key.Version_Id,
        TopPartId = g.Key.TopPartId,
        PlannedQty = g.Sum(x => x.Planned_Qty)
    }
).ToListAsync();

    return Ok(data);
}

[HttpGet("in-production-correct")]
public async Task<IActionResult> GetInProductionCorrect([FromQuery] int versionId)
{
    var query = _db.BatchProducts.AsQueryable();

if (versionId > 0)
    query = query.Where(x => x.Version_Id == versionId);

    var data = await (
    from bp in query

    join ptp in _db.ProductTopParts
        on bp.ProductToPart_ID equals ptp.Id

    where bp.IsActive
          && ptp.IsActive
          && bp.ProductToPart_ID != null

          && _db.Tasks
            .Join(_db.TopPartSteps,
                t => t.TopPartStep_ID,
                ts => ts.Id,
                (t, ts) => new { t, ts })
            .Where(x =>
                x.t.BatchProduct_ID == bp.ID &&
                x.t.IsActive &&
                x.ts.StepType == 1 &&
                x.ts.IsFinal)
            .Any(x => x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3)

        && _db.Tasks
            .Join(_db.TopPartSteps,
                t => t.TopPartStep_ID,
                ts => ts.Id,
                (t, ts) => new { t, ts })
            .Where(x =>
                x.t.BatchProduct_ID == bp.ID &&
                x.t.IsActive &&
                x.ts.StepType == 1 &&
                x.ts.IsFinal)
            .Any(x => x.t.Tasks_Status != 3)

    group bp by new
    {
        bp.Version_Id,
        ptp.TopPartId
    } into g

    select new
    {
        VersionId = g.Key.Version_Id,
        TopPartId = g.Key.TopPartId,
        InProduction = g.Sum(x => x.Planned_Qty)
    }
).ToListAsync();

    return Ok(data);
}

[HttpGet("ok-correct")]
public async Task<IActionResult> GetOkCorrect([FromQuery] int versionId)
{
    var query = _db.BatchProducts.AsQueryable();

    if (versionId > 0)
        query = query.Where(x => x.Version_Id == versionId);

    var data = await (
        from bp in query

        join ptp in _db.ProductTopParts
            on bp.ProductToPart_ID equals ptp.Id

        where bp.IsActive
              && ptp.IsActive
              && bp.ProductToPart_ID != null

              // VISI DETAIL taski = 3
              && !_db.Tasks
                    .Join(_db.TopPartSteps,
                        t => t.TopPartStep_ID,
                        ts => ts.Id,
                        (t, ts) => new { t, ts })
                    .Any(x =>
                        x.t.BatchProduct_ID == bp.ID &&
                        x.t.IsActive &&
                        x.ts.StepType == 1 &&
                        x.t.Tasks_Status != 3)

        group bp by new
        {
            bp.Version_Id,
            ptp.TopPartId
        } into g

        select new
        {
            VersionId = g.Key.Version_Id,
            TopPartId = g.Key.TopPartId,
            OkQty = g.Sum(x => x.Planned_Qty)
        }
    ).ToListAsync();

    return Ok(data);
}

[HttpGet("free-correct")]
public async Task<IActionResult> GetFreeCorrect([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

    var usedMap = await _db.Tasks
        .Join(_db.TopPartSteps,
            t => t.TopPartStep_ID,
            ts => ts.Id,
            (t, ts) => new { t, ts })
        .Where(x =>
                x.t.IsActive &&
                x.ts.StepType == 2 &&
                (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3) &&
                x.t.Qty_Done > 0
            )

        .GroupBy(x => new
        {
            x.t.BatchProduct_ID,
            x.ts.ProductToPartId
        })
        .Select(g => new
        {
            g.Key.BatchProduct_ID,
            g.Key.ProductToPartId,
            Used = g.Sum(x => (int?)x.t.Qty_Done) ?? 0
        })
        .ToListAsync();

    var usedDict = usedMap.ToDictionary(
        x => $"{x.BatchProduct_ID}_{x.ProductToPartId}",
        x => x.Used);

    var rawData = await (
    from bp in _db.BatchProducts
    where bp.IsActive
          && bp.Version_Id == versionId
          && bp.ProductToPart_ID == null

    from ptp in _db.ProductTopParts
    where ptp.VersionId == bp.Version_Id
          && ptp.IsActive

          && _db.Tasks
              .Join(_db.TopPartSteps,
                  t => t.TopPartStep_ID,
                  ts => ts.Id,
                  (t, ts) => new { t, ts })
              .Any(x =>
                  x.t.BatchProduct_ID == bp.ID &&
                  x.t.IsActive &&
                  x.ts.StepType == 1 &&
                  x.ts.ProductToPartId == ptp.Id)

          && !_db.Tasks
              .Join(_db.TopPartSteps,
                  t => t.TopPartStep_ID,
                  ts => ts.Id,
                  (t, ts) => new { t, ts })
              .Any(x =>
                  x.t.BatchProduct_ID == bp.ID &&
                  x.t.IsActive &&
                  x.ts.StepType == 2 &&
                  x.ts.ProductToPartId == ptp.Id &&
                  (x.t.Tasks_Status == 2 || x.t.Tasks_Status == 3))

    select new
    {
        bpId = bp.ID,
        VersionId = bp.Version_Id,
        ptp.TopPartId,
        ProductToPartId = ptp.Id,
        Qty = bp.Planned_Qty * ptp.QtyPerProduct
    }
).ToListAsync();

var data = rawData
    .GroupBy(x => new { x.VersionId, x.TopPartId })
    .Select(g => new
    {
        VersionId = g.Key.VersionId,
        TopPartId = g.Key.TopPartId,
        FreeQty = g.Sum(x =>
            x.Qty - (
                usedDict.TryGetValue($"{x.bpId}_{x.ProductToPartId}", out var used)
                    ? used
                    : 0))
    })
    .ToList();

    return Ok(data);
}

    }
}