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
    }
}