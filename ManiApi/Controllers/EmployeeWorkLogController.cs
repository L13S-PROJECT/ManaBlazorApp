using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeWorkLogController : ControllerBase
    {
        private readonly AppDbContext _db;

        public EmployeeWorkLogController(AppDbContext db)
        {
            _db = db;
        }

        // GET api/employeeworklog/date/2026-03-13
        [HttpGet("date/{date}")]
        public async Task<IActionResult> GetByDate(DateTime date)
        {
            var data = await _db.EmployeeWorkLogs
                .Where(x => x.WorkDate.Date == date.Date)
                .ToListAsync();

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Save(EmployeeWorkLog model)
        {
            var existing = await _db.EmployeeWorkLogs
                .FirstOrDefaultAsync(x => x.EmployeeID == model.EmployeeID && x.WorkDate == model.WorkDate);

            if (existing == null)
            {
                _db.EmployeeWorkLogs.Add(model);
            }
            else
            {
                existing.TimeFrom = model.TimeFrom;
                existing.TimeTo = model.TimeTo;
                existing.Hours = model.Hours;
                existing.Notes = model.Notes;
            }

            await _db.SaveChangesAsync();

            return Ok();
        }
    }
}