using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CategoriesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rows = await _db.Categories
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.CategoryName)
                .Select(x => new
                {
                    x.Id,
                    x.CategoryName,
                    x.ParentId
                })
                .ToListAsync();

            return Ok(rows);
        }
    }
}