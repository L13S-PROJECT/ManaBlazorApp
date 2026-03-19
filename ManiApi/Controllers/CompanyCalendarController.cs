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
            return await _context.CompanyCalendars.ToListAsync();
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

    }
    
}