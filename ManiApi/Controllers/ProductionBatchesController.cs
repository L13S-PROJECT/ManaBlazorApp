using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/production-batches")]
    public class ProductionBatchesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProductionBatchesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var batches = await _db.ProductionBatches
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.ID)
                .ToListAsync();

            return Ok(batches);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductionBatch dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Batch_Code))
                return BadRequest("Batch code is required.");

            var code = dto.Batch_Code.Trim();

            var exists = await _db.ProductionBatches
                .AnyAsync(x => x.Batch_Code == code);

            if (exists)
                return Conflict("Batch code already exists.");

            var batch = new ProductionBatch
            {
                Batch_Code = code,
                Batch_Status = 1,
                Start_Date = null,
                End_Date = null,
                Comments = dto.Comments,
                IsActive = true,
                Created_At = DateTime.UtcNow
            };

            _db.ProductionBatches.Add(batch);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                batch.ID,
                batch.Batch_Code
            });
        }

        [HttpPost("{batchId:int}/topparts")]
        public async Task<IActionResult> AddTopPart(
            int batchId,
            [FromBody] ProductionBatchTopPart dto)
        {
            var batchExists = await _db.ProductionBatches
                .AnyAsync(x => x.ID == batchId && x.IsActive);

            if (!batchExists)
                return NotFound("Production batch not found.");

            var workflow = await _db.Workflows
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.Workflow_ID &&
                    x.IsActive);

            if (workflow == null)
                return BadRequest("Workflow not found.");

            if (workflow.TopPartId != (uint)dto.TopPart_ID)
                return BadRequest("Workflow does not belong to selected TopPart.");

            var alreadyExists = await _db.ProductionBatchTopParts
                .AnyAsync(x =>
                    x.Batch_ID == (uint)batchId &&
                    x.TopPart_ID == dto.TopPart_ID &&
                    x.Workflow_ID == dto.Workflow_ID &&
                    x.IsActive);

            if (alreadyExists)
                return Conflict("This TopPart and Workflow already exist in the production batch.");

            var row = new ProductionBatchTopPart
            {
                Batch_ID = (uint)batchId,
                TopPart_ID = dto.TopPart_ID,
                Workflow_ID = dto.Workflow_ID,
                Planned_Qty = dto.Planned_Qty,
                Done_Qty = 0,
                IsPriority = dto.IsPriority,
                Comments = dto.Comments,
                IsActive = true
            };

            _db.ProductionBatchTopParts.Add(row);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                row.ID,
                row.Batch_ID,
                row.TopPart_ID,
                row.Workflow_ID,
                row.Planned_Qty
            });
        }

        [HttpGet("{batchId:int}/topparts")]
        public async Task<IActionResult> GetTopParts(int batchId)
        {
            var rows = await _db.ProductionBatchTopParts
                .AsNoTracking()
                .Where(x => x.Batch_ID == (uint)batchId && x.IsActive)
                .Include(x => x.TopPart)
                .Include(x => x.Workflow)
                .OrderBy(x => x.ID)
                .ToListAsync();

            return Ok(rows);
        }

    }
}