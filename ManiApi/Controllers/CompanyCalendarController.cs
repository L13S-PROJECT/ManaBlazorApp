using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyCalendarController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CompanyCalendarController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
public async Task<ActionResult<IEnumerable<CompanyCalendar>>> Get()
{
var data = await _context.CompanyCalendars
    .Select(day => new CompanyCalendar
    {
        WorkDate = day.WorkDate,
        WorkStart = day.WorkStart,
        WorkEnd = day.WorkEnd,
        BreakMinutes = day.BreakMinutes,
        Notes = day.Notes,
        Breaks = _context.CompanyCalendarBreaks
            .Where(b => b.WorkDate == day.WorkDate)
            .Select(b => new CompanyCalendarBreak
            {
                WorkDate = b.WorkDate,
                BreakStart = b.BreakStart,
                BreakEnd = b.BreakEnd,
                IsActive = b.IsActive
            }).ToList()
    })
    .ToListAsync();

    return Ok(data);
}

        [HttpPost]
    public async Task<IActionResult> Post(CompanyCalendar model)
    {
        var existing = await _context.CompanyCalendars
            .FirstOrDefaultAsync(x => x.WorkDate == model.WorkDate);

        if (existing == null)
        {
            _context.CompanyCalendars.Add(model);
        }
        else
        {
            existing.WorkStart = model.WorkStart;
            existing.WorkEnd = model.WorkEnd;
            existing.BreakMinutes = model.BreakMinutes;
            existing.Notes = model.Notes;
        }
// izdzēš vecos breakus
var existingBreaks = _context.CompanyCalendarBreaks
    .Where(x => x.WorkDate == model.WorkDate);

_context.CompanyCalendarBreaks.RemoveRange(existingBreaks);

// pievieno jaunus
if (model.Breaks != null)
{
    foreach (var b in model.Breaks)
    {
        _context.CompanyCalendarBreaks.Add(new CompanyCalendarBreak
        {
            WorkDate = model.WorkDate,
            BreakStart = b.BreakStart,
            BreakEnd = b.BreakEnd,
            IsActive = b.IsActive
        });
    }
}

await _context.SaveChangesAsync();
    
        return Ok();   
    }

    [HttpDelete("{date}")]
public async Task<IActionResult> Delete(DateTime date)
{
    var existing = await _context.CompanyCalendars
        .FirstOrDefaultAsync(x => x.WorkDate == date);

    if (existing == null)
        return NotFound();

    _context.CompanyCalendars.Remove(existing);
    await _context.SaveChangesAsync();

    return Ok();
}

class BreakDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

    }
    
}