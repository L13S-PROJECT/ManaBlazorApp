using ManaApp.Shared.DTOs.Production;
using ManiApi.Data;
using ManiApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/production-component-staging")]
    public class ProductionComponentStagingController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ProductionComponentStagingController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("{productionExecutionId:int}")]
        public async Task<ActionResult<List<ProductionComponentStagingDto>>> GetAll(
            int productionExecutionId)
        {
            if (productionExecutionId <= 0)
                return BadRequest("ProductionExecution ID nav derīgs.");

            var executionExists = await _db.ProductionExecutions
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ID == (uint)productionExecutionId &&
                    x.IsActive);

            if (!executionExists)
                return NotFound("ProductionExecution nav atrasts.");

            var rows = await (
                from staging in _db.ProductionComponentStagings.AsNoTracking()

                join processComponent in
                    _db.WorkflowProcessComponents.AsNoTracking()
                    on staging.WorkflowProcessComponent_ID
                    equals processComponent.Id

                join workflowComponent in
                    _db.WorkflowComponents.AsNoTracking()
                    on processComponent.WorkflowComponentId
                    equals workflowComponent.Id

                join topPart in _db.TopParts.AsNoTracking()
                    on workflowComponent.TopPartId equals (uint?)topPart.Id

                where
                    staging.ProductionExecution_ID ==
                        (uint)productionExecutionId &&
                    staging.IsActive &&
                    workflowComponent.IsActive &&
                    workflowComponent.ComponentType == 1 &&
                    topPart.IsActive

                group staging by new
                {
                    topPart.Id,
                    topPart.TopPartCode,
                    topPart.TopPartName
                }
                into componentGroup

                orderby componentGroup.Key.TopPartCode

                select new ProductionComponentStagingDto
                {
                    ProductionExecutionId =
                        (uint)productionExecutionId,
                    TopPartId = componentGroup.Key.Id,
                    TopPartCode = componentGroup.Key.TopPartCode,
                    TopPartName = componentGroup.Key.TopPartName,
                    RequiredQuantity =
                        componentGroup.Sum(x => x.RequiredQuantity),
                    StagedQuantity =
                        componentGroup.Sum(x => x.StagedQuantity)
                }
            ).ToListAsync();

            return Ok(rows);
        }

        [HttpPut("{productionExecutionId:int}/components/{topPartId:int}")]
            public async Task<IActionResult> UpdateComponent(
                int productionExecutionId,
                int topPartId,
                [FromBody] UpdateProductionComponentStagingRequest dto)
            {
                if (productionExecutionId <= 0 || topPartId <= 0)
                    return BadRequest("ID nav derīgs.");

                if (dto.StagedQuantity < 0)
                    return BadRequest(
                        "Sakomplektētais daudzums nedrīkst būt negatīvs.");

                var execution = await _db.ProductionExecutions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ID == (uint)productionExecutionId &&
                        x.IsActive);

                if (execution == null)
                    return NotFound("ProductionExecution nav atrasts.");

                if (execution.Status == ProductionExecutionStatus.COMPLETED ||
                    execution.Status == ProductionExecutionStatus.SCRAPPED)
                {
                    return BadRequest(
                        "Pabeigtai vai norakstītai izpildei komplektāciju mainīt nedrīkst.");
                }

                var employeeExists = await _db.Employees
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == dto.StagedByEmployeeId &&
                        x.IsActive);

                if (!employeeExists)
                    return BadRequest("Darbinieks nav atrasts vai nav aktīvs.");

                var stagingRows = await (
                    from staging in _db.ProductionComponentStagings

                    join processComponent in _db.WorkflowProcessComponents
                        on staging.WorkflowProcessComponent_ID
                        equals processComponent.Id

                    join workflowComponent in _db.WorkflowComponents
                        on processComponent.WorkflowComponentId
                        equals workflowComponent.Id

                    join processNode in _db.WorkflowNodes
                        on processComponent.ProcessNodeId equals processNode.Id

                    where
                        staging.ProductionExecution_ID ==
                            (uint)productionExecutionId &&
                        staging.IsActive &&
                        workflowComponent.IsActive &&
                        workflowComponent.ComponentType == 1 &&
                        workflowComponent.TopPartId == (uint)topPartId &&
                        processNode.IsActive

                    orderby processNode.SortOrder

                    select staging
                ).ToListAsync();

                if (stagingRows.Count == 0)
                    return NotFound("Komponente šajā paletē nav atrasta.");

                var requiredQuantity = stagingRows
                    .Sum(x => x.RequiredQuantity);

                if (dto.StagedQuantity > requiredQuantity)
                    return BadRequest(
                        $"Sakomplektētais daudzums nedrīkst pārsniegt {requiredQuantity}.");

                var remainingQuantity = dto.StagedQuantity;
                var now = DateTime.UtcNow;

                foreach (var staging in stagingRows)
                {
                    var rowQuantity = Math.Min(
                        staging.RequiredQuantity,
                        remainingQuantity);

                    staging.StagedQuantity = rowQuantity;
                    staging.StagedByEmployee_ID =
                        rowQuantity > 0 ? dto.StagedByEmployeeId : null;
                    staging.Staged_At =
                        rowQuantity > 0 ? now : null;

                    remainingQuantity -= rowQuantity;
                }

                await _db.SaveChangesAsync();

                return NoContent();
            }
    }
}