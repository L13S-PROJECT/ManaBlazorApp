using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManaApp.Shared.DTOs.TopPart;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopPartCategoriesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TopPartCategoriesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var rows = await _db.TopPartCategories
                .Where(x => x.IsActive)
                .OrderBy(x => x.CategoryName)
                .Select(x => new TopPartCategoryDto
                        {
                            Id = x.Id,
                            CategoryName = x.CategoryName
                        })
                .ToListAsync();

            return Ok(rows);
        }
    }
}