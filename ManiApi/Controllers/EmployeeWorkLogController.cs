
// EmployeeWorkLogController.cs - API kontrolieris, kas apstrādā pieprasījumus saistībā ar darbinieku darba laikiem

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
                .Where(x => x.WorkDate >= date.Date && x.WorkDate < date.Date.AddDays(1))
                .ToListAsync();

            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Save(EmployeeWorkLog model)
        {
        var workDate = model.WorkDate.Date;

        var existing = await _db.EmployeeWorkLogs
            .FirstOrDefaultAsync(x =>
                x.EmployeeID == model.EmployeeID &&
                x.WorkDate.Date == workDate);

            if (existing == null)
            {
                if (model.TimeFrom.HasValue && model.TimeTo.HasValue)
                {
                    var hours = (decimal)(model.TimeTo.Value - model.TimeFrom.Value).TotalHours;
                    var breakMinutes = model.BreakMinutes ?? 0;

                    model.Hours = hours - (breakMinutes / 60m);
                }
                model.WorkDate = workDate;
                _db.EmployeeWorkLogs.Add(model);
            }
            else
            {
                existing.WorkDate = workDate;
                existing.TimeFrom = model.TimeFrom;
                existing.TimeTo = model.TimeTo;
                if (model.TimeFrom.HasValue && model.TimeTo.HasValue)
                    {
                        var hours = (decimal)(model.TimeTo.Value - model.TimeFrom.Value).TotalHours;
                        var breakMinutes = model.BreakMinutes ?? 0;

                        existing.Hours = hours - (breakMinutes / 60m);
                    }
                    else
                    {
                        existing.Hours = model.Hours;
                    }
                existing.Notes = model.Notes;
                existing.BreakMinutes = model.BreakMinutes;
                existing.BreaksJson = model.BreaksJson;
            }

            await _db.SaveChangesAsync();

            return Ok();
        }

    [HttpDelete("date/{date}")]
public async Task<IActionResult> DeleteByDate(DateTime date)
{
var items = await _db.EmployeeWorkLogs
    .Where(x => x.WorkDate >= date.Date && x.WorkDate < date.Date.AddDays(1))
    .ToListAsync();

    _db.EmployeeWorkLogs.RemoveRange(items);
    await _db.SaveChangesAsync();

    return Ok();
}

[HttpGet("range")]
public async Task<IActionResult> GetRange(DateTime from, DateTime to)
{
    var data = await _db.EmployeeWorkLogs
        .Where(x => x.WorkDate >= from.Date && x.WorkDate < to.Date.AddDays(1))
        .ToListAsync();

    return Ok(data);
}

    }
}