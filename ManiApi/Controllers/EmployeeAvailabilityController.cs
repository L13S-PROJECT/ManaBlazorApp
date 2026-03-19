using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeAvailabilityController : ControllerBase
    {
        private readonly AppDbContext _db;

        public EmployeeAvailabilityController(AppDbContext db)
        {
            _db = db;
        }

        // GET api/employeeavailability/range?from=2026-03-01&to=2026-03-31
        [HttpGet("range")]
        public async Task<IActionResult> GetRange(DateTime from, DateTime to)
        {
            var data = await _db.EmployeeAvailabilities
                .Where(x => x.DateFrom <= to && (x.DateTo == null || x.DateTo >= from))
                .ToListAsync();

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Save(EmployeeAvailability model)
        {
            if (model.ID == 0)
            {
                _db.EmployeeAvailabilities.Add(model);
            }
            else
            {
                var existing = await _db.EmployeeAvailabilities.FindAsync(model.ID);

                if (existing == null)
                    return NotFound();

                existing.DateFrom = model.DateFrom;
                existing.DateTo = model.DateTo;
                existing.Status = model.Status;
                existing.Notes = model.Notes;
                existing.Hours = model.Hours;
            }

            await _db.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("{id}")]
            public async Task<IActionResult> Delete(int id)
            {
                var item = await _db.EmployeeAvailabilities.FindAsync(id);

                if (item == null)
                    return NotFound();

                _db.EmployeeAvailabilities.Remove(item);

                await _db.SaveChangesAsync();

                return Ok();
            }
    }
}