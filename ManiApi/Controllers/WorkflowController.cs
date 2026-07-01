using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models; 
using ManiApi.DTOs.WorkFlow;


namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowController : ControllerBase
    {
        private readonly AppDbContext _db;

        public WorkflowController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("{versionId}")]
        public async Task<IActionResult> GetWorkflow(int versionId)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(x =>
                    x.VersionId == versionId &&
                    x.IsActive);

            if (workflow == null)
                return NotFound("Workflow nav atrasts.");

            var nodes = await _db.WorkflowNodes
                .Where(x =>
                    x.WorkflowId == workflow.Id &&
                    x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var connections = await _db.WorkflowNodeConnections
                .Where(x =>
                    nodes.Select(n => n.Id).Contains(x.FromNodeId) ||
                    nodes.Select(n => n.Id).Contains(x.ToNodeId))
                .ToListAsync();

            return Ok(new
            {
                Workflow = workflow,
                Nodes = nodes,
                Connections = connections
            });
        }

        [HttpGet("parts/{versionId}")]
            public async Task<IActionResult> GetParts(int versionId)
            {
                var parts = await _db.ProductTopParts
                    .Where(x => x.VersionId == versionId && x.IsActive)
                    .Join(_db.TopParts.Where(tp => tp.IsActive),
                        pt => pt.TopPartId,
                        tp => tp.Id,
                        (pt, tp) => new WorkflowPartDto
                        {
                            ProductToPartId = pt.Id,
                            TopPartId = tp.Id,
                            TopPartCode = tp.TopPartCode,
                            TopPartName = tp.TopPartName,
                            QtyPerProduct = pt.QtyPerProduct
                        })
                    .OrderBy(x => x.TopPartName)
                    .ToListAsync();

                return Ok(parts);
            }

        [HttpPost]
        public async Task<IActionResult> CreateWorkflow(CreateWorkflowRequest dto)
        {
            var exists = await _db.Workflows
                .AnyAsync(x =>
                    x.VersionId == dto.VersionId &&
                    x.IsActive);

            if (exists)
                return BadRequest("Šai versijai Workflow jau eksistē.");

            var workflow = new Workflow
            {
                VersionId = dto.VersionId,
                ParentNodeId = null,
                Name = dto.WorkflowName,
                IsActive = true
            };

            _db.Workflows.Add(workflow);

            await _db.SaveChangesAsync();

            return Ok(workflow);
        }

        [HttpGet("topparts")]
            public async Task<IActionResult> GetTopParts()
            {
                var parts = await _db.TopParts
                    .Where(x => x.IsActive && x.Stage == 1)
                    .OrderBy(x => x.TopPartName)
                    .Select(x => new WorkflowPartDto
                    {
                        TopPartId = x.Id,
                        TopPartCode = x.TopPartCode,
                        TopPartName = x.TopPartName,
                        Stage = x.Stage
                    })
                    .ToListAsync();

                return Ok(parts);
            }


        [HttpPost("node")]
        public async Task<IActionResult> CreateNode(CreateWorkflowNodeRequest dto)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(x => x.Id == dto.WorkflowId && x.IsActive);

            if (workflow == null)
                return NotFound("Workflow nav atrasts.");
            
            ProductTopPart? productPart = null;

            if (dto.NodeType == 1)
                {
                    if (dto.TopPartId is null)
                        return BadRequest("TopPart nav norādīts.");

                    productPart = await _db.ProductTopParts
                        .FirstOrDefaultAsync(x =>
                            x.VersionId == workflow.VersionId &&
                            x.TopPartId == dto.TopPartId &&
                            x.IsActive);

                    if (productPart == null)
                    {
                        productPart = new ProductTopPart
                        {
                            VersionId = workflow.VersionId,
                            TopPartId = dto.TopPartId.Value,
                            QtyPerProduct = 1,
                            IsActive = true
                        };

                        _db.ProductTopParts.Add(productPart);
                        await _db.SaveChangesAsync();
                    }
                }

            var maxSort = await _db.WorkflowNodes
                .Where(x => x.WorkflowId == dto.WorkflowId)
                .MaxAsync(x => (int?)x.SortOrder) ?? 0;

            var node = new WorkflowNode
            {
                WorkflowId = dto.WorkflowId,
                NodeType = dto.NodeType,
                Name = dto.Name,
                ProductToPartId = productPart!.Id,
                WorkCenterId = dto.WorkCenterId,
                EstimatedMinutes = dto.EstimatedMinutes,
                Comments = dto.Comments,
                SortOrder = maxSort + 10,
                IsActive = true
            };

            _db.WorkflowNodes.Add(node);

            await _db.SaveChangesAsync();

            return Ok(node);
        }

        [HttpPost("connect")]
        public async Task<IActionResult> CreateConnection(CreateWorkflowConnectionRequest dto)
        {
            var fromNode = await _db.WorkflowNodes
                .FirstOrDefaultAsync(x => x.Id == dto.FromNodeId && x.IsActive);

            if (fromNode == null)
                return BadRequest("FromNode nav atrasts.");

            var toNode = await _db.WorkflowNodes
                .FirstOrDefaultAsync(x => x.Id == dto.ToNodeId && x.IsActive);

            if (toNode == null)
                return BadRequest("ToNode nav atrasts.");

            if (fromNode.WorkflowId != toNode.WorkflowId)
                return BadRequest("Node pieder dažādiem Workflow.");

            var exists = await _db.WorkflowNodeConnections
                .AnyAsync(x =>
                    x.FromNodeId == dto.FromNodeId &&
                    x.ToNodeId == dto.ToNodeId);

            if (exists)
                return BadRequest("Savienojums jau eksistē.");

            var connection = new WorkflowNodeConnection
            {
                FromNodeId = dto.FromNodeId,
                ToNodeId = dto.ToNodeId
            };

            _db.WorkflowNodeConnections.Add(connection);

            await _db.SaveChangesAsync();

            return Ok(connection);
        }

        [HttpPost("parts/save")]
        public async Task<IActionResult> SaveParts(SaveWorkflowPartsRequest dto)
            {
                    if (dto.Parts.Count == 0)
                        {
                            await _db.ProductTopParts
                                .Where(x => x.VersionId == dto.VersionId)
                                .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsActive, false));

                            return Ok();
                        }
                    
                    var existing = await _db.ProductTopParts
                        .Where(x => x.VersionId == dto.VersionId)
                        .ToListAsync();

                    foreach (var item in dto.Parts)
                        {
                            var part = existing.FirstOrDefault(x => x.TopPartId == item.TopPartId);

                            if (part == null)
                            {
                                _db.ProductTopParts.Add(new ProductTopPart
                                {
                                    VersionId = dto.VersionId,
                                    TopPartId = item.TopPartId,
                                    QtyPerProduct = item.QtyPerProduct,
                                    IsActive = true
                                });
                            }
                        else
                            {
                                part.IsActive = true;
                                part.QtyPerProduct = item.QtyPerProduct;
                            }
                        }

                    foreach (var part in existing)
                        {
                            part.IsActive = dto.Parts.Any(x => x.TopPartId == part.TopPartId);
                        }

                    await _db.SaveChangesAsync();

                    return Ok();
            }

    }
}