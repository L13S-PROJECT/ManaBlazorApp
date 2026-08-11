// itemsControllers.cs

using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManaApp.Shared.DTOs.Items;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ItemsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ItemsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("list")]
        public async Task<ActionResult<List<ItemListDto>>> GetList()
        {
            var rows = await (
                from i in _db.Items.AsNoTracking()
                join t in _db.ItemTypes on i.ItemTypeId equals t.Id
                where i.IsActive && t.IsActive
                orderby t.SortOrder, i.ItemCode
                select new ItemListDto
                {
                    Id = i.Id,
                    ItemTypeId = t.Id,
                    ItemTypeName = t.TypeName,
                    ItemCode = i.ItemCode,
                    ItemName = i.ItemName,
                    Unit = i.Unit
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("selector")]
        public async Task<ActionResult<List<ItemSelectorDto>>> GetSelector()
        {
            var rows = await (
                from i in _db.Items.AsNoTracking()
                join t in _db.ItemTypes on i.ItemTypeId equals t.Id
                where i.IsActive && t.IsActive
                orderby t.SortOrder, i.ItemCode
                select new ItemSelectorDto
                {
                    Id = i.Id,
                    Text = $"{i.ItemCode} - {i.ItemName} ({i.Unit})"
                })
                .ToListAsync();

            return Ok(rows);
        }

        [HttpGet("{id:int}")]
            public async Task<ActionResult<ItemEditDto>> Get(int id)
            {
                var item = await _db.Items
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(x => new ItemEditDto
                    {
                        Id = x.Id,
                        ItemTypeId = x.ItemTypeId,
                        ItemCode = x.ItemCode,
                        ItemName = x.ItemName,
                        Description = x.Description,
                        Unit = x.Unit,
                        IsActive = x.IsActive
                    })
                    .FirstOrDefaultAsync();

                if (item == null)
                    return NotFound();

                return Ok(item);
            }

            [HttpGet("types")]
                public async Task<ActionResult<List<ItemTypeDto>>> GetTypes()
                {
                    var rows = await _db.ItemTypes
                        .AsNoTracking()
                        .Where(x => x.IsActive)
                        .OrderBy(x => x.SortOrder)
                        .Select(x => new ItemTypeDto
                        {
                            Id = x.Id,
                            TypeName = x.TypeName
                        })
                        .ToListAsync();

                    return Ok(rows);
                }

            [HttpPost]
                public async Task<ActionResult<int>> Create(ItemEditDto dto)
                {
                    dto.ItemCode = dto.ItemCode.Trim();
                    dto.ItemName = dto.ItemName.Trim();
                    dto.Unit = dto.Unit.Trim();
                    dto.Description = dto.Description?.Trim();
                    
                    if (string.IsNullOrWhiteSpace(dto.ItemCode))
                        return BadRequest("Nav norādīts kods.");

                    if (string.IsNullOrWhiteSpace(dto.ItemName))
                        return BadRequest("Nav norādīts nosaukums.");

                    if (dto.ItemTypeId <= 0)
                        return BadRequest("Nav izvēlēts tips.");

                    if (string.IsNullOrWhiteSpace(dto.Unit))
                        return BadRequest("Nav izvēlēta mērvienība.");

                    if (await _db.Items.AnyAsync(x =>
                        x.ItemCode.ToUpper() == dto.ItemCode.ToUpper() &&
                        x.IsActive))
                    {
                        return BadRequest("Kods jau eksistē.");
                    }
                    
                    var item = new Models.Item
                    {
                        ItemTypeId = dto.ItemTypeId,
                        ItemCode = dto.ItemCode,
                        ItemName = dto.ItemName,
                        Description = dto.Description,
                        Unit = dto.Unit,
                        IsActive = dto.IsActive
                    };

                    _db.Items.Add(item);
                    await _db.SaveChangesAsync();

                    return Ok(item.Id);
                }

            [HttpPut("{id:int}")]
                public async Task<IActionResult> Update(int id, ItemEditDto dto)
                {
                    dto.ItemCode = dto.ItemCode.Trim();
                    dto.ItemName = dto.ItemName.Trim();
                    dto.Unit = dto.Unit.Trim();
                    dto.Description = dto.Description?.Trim();
                    
                    if (string.IsNullOrWhiteSpace(dto.ItemCode))
                        return BadRequest("Nav norādīts kods.");

                    if (string.IsNullOrWhiteSpace(dto.ItemName))
                        return BadRequest("Nav norādīts nosaukums.");

                    if (dto.ItemTypeId <= 0)
                        return BadRequest("Nav izvēlēts tips.");

                    if (string.IsNullOrWhiteSpace(dto.Unit))
                        return BadRequest("Nav izvēlēta mērvienība.");

                    if (await _db.Items.AnyAsync(x =>
                        x.Id != id &&
                        x.ItemCode.ToUpper() == dto.ItemCode.ToUpper() &&
                        x.IsActive))
                    {
                        return BadRequest("Kods jau eksistē.");
                    }
                    
                    var item = await _db.Items.FindAsync(id);

                    if (item == null)
                        return NotFound();

                    item.ItemTypeId = dto.ItemTypeId;
                    item.ItemCode = dto.ItemCode;
                    item.ItemName = dto.ItemName;
                    item.Description = dto.Description;
                    item.Unit = dto.Unit;
                    item.IsActive = dto.IsActive;

                    await _db.SaveChangesAsync();

                    return NoContent();
                }

        [HttpDelete("{id:int}")]
            public async Task<IActionResult> Delete(int id)
            {
                var item = await _db.Items.FindAsync(id);

                if (item == null)
                    return NotFound();

                item.IsActive = false;

                await _db.SaveChangesAsync();

                return NoContent();
            }

        [HttpGet("units")]
            public async Task<ActionResult<List<UnitDto>>> GetUnits()
            {
                var rows = await _db.Units
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new UnitDto
                    {
                        Id = x.Id,
                        UnitCode = x.UnitCode,
                        UnitName = x.UnitName
                    })
                    .ToListAsync();

                return Ok(rows);
            }

    }
}
