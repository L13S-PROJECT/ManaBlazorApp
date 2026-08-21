using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/stock-new")]
    public class StockMovementsNewController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StockMovementsNewController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost("production/{productionBatchTopPartId:int}")]
        public async Task<IActionResult> AddProduction(
            int productionBatchTopPartId,
            [FromBody] int quantity)
        {
            if (quantity <= 0)
                return BadRequest("Quantity must be greater than 0.");

            var batchTopPart = await _db.ProductionBatchTopParts
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ID == (uint)productionBatchTopPartId &&
                    x.IsActive);

            if (batchTopPart == null)
                return NotFound("Production batch TopPart not found.");

            var movement = new StockMovementNew
            {
                TopPart_ID = batchTopPart.TopPart_ID,
                ProductionBatchTopPart_ID = batchTopPart.ID,
                Movement_Type = "PRODUCTION",
                Quantity = quantity,
                Created_At = DateTime.UtcNow,
                IsActive = true
            };

            _db.StockMovementsNew.Add(movement);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                movement.ID,
                movement.TopPart_ID,
                movement.ProductionBatchTopPart_ID,
                movement.Quantity
            });
        }

        [HttpGet("balance/{topPartId:int}")]
        public async Task<IActionResult> GetBalance(int topPartId)
        {
            var balance = await _db.StockMovementsNew
                .AsNoTracking()
                .Where(x =>
                    x.TopPart_ID == topPartId &&
                    x.IsActive)
                .SumAsync(x => x.Quantity);

            return Ok(new
            {
                TopPart_ID = topPartId,
                Quantity = balance
            });
        }

    }

    
}