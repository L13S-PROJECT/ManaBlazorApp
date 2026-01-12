using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public EmployeesController(AppDbContext db) => _db = db;

        // POST: /api/employees/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Nepareizi dati.");

            var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT ID, Employee_Name
FROM employees
WHERE UserName = @u AND Password = @p AND IsActive = 1
LIMIT 1;";
            var pu = cmd.CreateParameter(); pu.ParameterName = "@u"; pu.Value = dto.Username; cmd.Parameters.Add(pu);
            var pp = cmd.CreateParameter(); pp.ParameterName = "@p"; pp.Value = dto.Password; cmd.Parameters.Add(pp);

            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Unauthorized();

            var id   = r.GetInt32(0);
            var name = r.GetString(1);
            return Ok(new { id, name });
        }

        public sealed class LoginDto
        {
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
