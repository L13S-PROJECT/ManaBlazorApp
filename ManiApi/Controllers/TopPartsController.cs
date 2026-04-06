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
                    on bp.ProductTopPart_Id equals ptp.Id into ptpGroup
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
                    TopPartId = tp != null ? tp.Id : bp.ProductTopPart_Id 
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

    }
}