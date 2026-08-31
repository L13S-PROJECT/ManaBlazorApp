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
                Movement_Type = StockMovementType.PRODUCTION,
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

        [HttpPost("reversal/{sourceMovementId:int}")]
        public async Task<IActionResult> AddReversal(int sourceMovementId)
        {
            var source = await _db.StockMovementsNew
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ID == (uint)sourceMovementId &&
                    x.IsActive);

            if (source == null)
                return NotFound("Stock movement not found.");

            var alreadyReversed = await _db.StockMovementsNew
                .AnyAsync(x => x.SourceMovement_ID == source.ID);

            if (alreadyReversed)
                return Conflict("Stock movement is already reversed.");

            var reversal = new StockMovementNew
            {
                TopPart_ID = source.TopPart_ID,
                ProductionBatchTopPart_ID = source.ProductionBatchTopPart_ID,
                WorkflowNode_ID = source.WorkflowNode_ID,
                RAL_Color_ID = source.RAL_Color_ID,
                Movement_Type = StockMovementType.REVERSAL,
                Quantity = -source.Quantity,
                SourceMovement_ID = source.ID,
                ConsumedByBatch_ID = source.ConsumedByBatch_ID,
                Created_At = DateTime.UtcNow,
                IsActive = true
            };

            _db.StockMovementsNew.Add(reversal);

            try
                {
                    await _db.SaveChangesAsync();
                }
            catch (DbUpdateException)
                {
                    var reversalNowExists = await _db.StockMovementsNew
                        .AsNoTracking()
                        .AnyAsync(x => x.SourceMovement_ID == source.ID);

                    if (reversalNowExists)
                        return Conflict("Stock movement is already reversed.");

                    throw;
                }

            return Ok(reversal);
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