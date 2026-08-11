using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;
using ManiApi.DTOs.ProductTopPartItems;
using ManaApp.Shared.DTOs.Items;


namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductTopPartItemsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProductTopPartItemsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("list")]
            public async Task<ActionResult<List<ProductTopPartItemDto>>> Get([FromQuery] int productToPartId)
            {
                var rows = await (
                    from ppi in _db.ProductTopPartItems.AsNoTracking()
                    join i in _db.Items on ppi.ItemId equals i.Id
                    join it in _db.ItemTypes on i.ItemTypeId equals it.Id
                    where ppi.ProductTopPartId == productToPartId
                        && ppi.IsActive
                        && i.IsActive
                        && it.IsActive
                    orderby ppi.SortOrder
                    select new ProductTopPartItemDto
                    {
                        Id = ppi.Id,
                        ItemId = i.Id,
                        ItemCode = i.ItemCode,
                        ItemName = i.ItemName,
                        Unit = i.Unit,
                        Qty = ppi.Qty,
                        SortOrder = ppi.SortOrder,
                        ItemTypeId = it.Id,
                        ItemTypeName = it.TypeName
                    })
                    .ToListAsync();

                return Ok(rows);
            }
            

        [HttpPost("create")]
            public async Task<IActionResult> Create([FromBody] CreateProductTopPartItemRequest dto)
            {
                if (dto.ProductTopPartId <= 0)
                    return BadRequest("ProductTopPartId is required.");

                if (dto.ItemId <= 0)
                    return BadRequest("ItemId is required.");

                if (dto.Qty <= 0)
                    return BadRequest("Qty must be greater than zero.");

                var partExists = await _db.ProductTopParts
                    .AnyAsync(x => x.Id == dto.ProductTopPartId && x.IsActive);

                if (!partExists)
                    return NotFound("ProductTopPart not found.");

                var itemExists = await _db.Items
                    .AnyAsync(x => x.Id == dto.ItemId && x.IsActive);

                if (!itemExists)
                    return NotFound("Item not found.");

                var existing = await _db.ProductTopPartItems
                    .FirstOrDefaultAsync(x =>
                        x.ProductTopPartId == dto.ProductTopPartId &&
                        x.ItemId == dto.ItemId);

                if (existing != null)
                {
                    if (!existing.IsActive)
                    {
                        existing.IsActive = true;
                        existing.Qty = dto.Qty;

                        await _db.SaveChangesAsync();

                        return Ok(new { existing.Id, Reactivated = true });
                    }

                    return Conflict("Šis materiāls jau ir pievienots.");
                }

                var sortOrder = await _db.ProductTopPartItems
                    .Where(x => x.ProductTopPartId == dto.ProductTopPartId && x.IsActive)
                    .MaxAsync(x => (int?)x.SortOrder) ?? 0;

                var row = new ProductTopPartItem
                {
                    ProductTopPartId = (uint)dto.ProductTopPartId,
                    ItemId = dto.ItemId,
                    Qty = dto.Qty,
                    SortOrder = sortOrder + 10,
                    IsActive = true
                };

                _db.ProductTopPartItems.Add(row);
                await _db.SaveChangesAsync();

                return Ok(new { row.Id });
            }

        [HttpDelete("delete/{id:int}")]
            public async Task<IActionResult> Delete(int id)
            {
                var row = await _db.ProductTopPartItems
                    .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

                if (row is null)
                    return NotFound();

                row.IsActive = false;

                await _db.SaveChangesAsync();

                return Ok();
            }

        [HttpPut("update")]
            public async Task<IActionResult> Update([FromBody] UpdateProductTopPartItemsRequest dto)
            {
                if (dto.Rows == null || dto.Rows.Count == 0)
                    return BadRequest("Nav datu.");

                var ids = dto.Rows.Select(x => x.Id).ToList();

                var rows = await _db.ProductTopPartItems
                    .Where(x => ids.Contains(x.Id) && x.IsActive)
                    .ToListAsync();

                if (rows.Count != ids.Count)
                    return BadRequest("Daži ieraksti nav atrasti.");

                foreach (var row in rows)
                {
                    var src = dto.Rows.First(x => x.Id == row.Id);

                    if (src.Qty <= 0)
                        return BadRequest("Qty must be greater than zero.");

                    row.Qty = src.Qty;
                    row.SortOrder = src.SortOrder;
                }

                await _db.SaveChangesAsync();

                return Ok();
            }
    }
}
