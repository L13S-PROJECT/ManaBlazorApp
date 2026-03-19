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
    var data = await _context.CompanyCalendars.ToListAsync();

    var allBreaks = await _context.CompanyCalendarBreaks
        .Where(x => x.IsActive)
        .ToListAsync();

    foreach (var day in data)
    {
        var breaks = allBreaks
            .Where(x => x.WorkDate == day.WorkDate)
            .Select(x => new
            {
                From = x.BreakStart.ToString(@"hh\:mm"),
                To = x.BreakEnd.ToString(@"hh\:mm")
            })
            .ToList();

        if (breaks.Any())
        {
            day.BreaksJson = System.Text.Json.JsonSerializer.Serialize(breaks);
        }
    }

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
            existing.BreaksJson = model.BreaksJson;
        }

        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(model.BreaksJson))
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<BreakDto>>(model.BreaksJson);

                if (parsed != null)
                {
                    // izdzēš vecos breakus šai dienai
                    var existingBreaks = _context.CompanyCalendarBreaks
                        .Where(x => x.WorkDate == model.WorkDate);

                    _context.CompanyCalendarBreaks.RemoveRange(existingBreaks);

                    // pievieno jaunus
                    foreach (var b in parsed)
                    {
                        _context.CompanyCalendarBreaks.Add(new CompanyCalendarBreak
                        {
                            WorkDate = model.WorkDate,
                            BreakStart = TimeSpan.Parse(b.From),
                            BreakEnd = TimeSpan.Parse(b.To),
                            IsActive = b.IsActive
                        });
                    }

                    await _context.SaveChangesAsync();
                }
            }
        model.BreaksJson = null;
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