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
        {
            workflow = new Workflow
            {
                VersionId = versionId,
                ParentNodeId = null,
                Name = $"Workflow {versionId}",
                IsActive = true
            };

            _db.Workflows.Add(workflow);

            await _db.SaveChangesAsync();
        }

        
        var nodes = await _db.WorkflowNodes
            .Where(x =>
                x.WorkflowId == workflow.Id &&
                x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new WorkflowNodeDto
            {
                Id = x.Id,
                WorkflowId = x.WorkflowId,
                NodeType = x.NodeType,
                Name = x.Name,
                ProductToPartId = x.ProductToPartId,
                WorkCenterId = x.WorkCenterId,
                EstimatedMinutes = x.EstimatedMinutes,
                Comments = x.Comments,
                SortOrder = x.SortOrder
            })
            .ToListAsync();

Console.WriteLine($"Nodes count = {nodes.Count}");

foreach (var n in nodes)
{
    Console.WriteLine(
        $"NodeId={n.Id}, Type={n.NodeType}, ProductToPartId={n.ProductToPartId}");
}         
            var productParts = await _db.ProductTopParts
                .Where(x =>
                    x.VersionId == workflow.VersionId &&
                    x.IsActive)
                .Join(_db.TopParts.Where(tp => tp.IsActive),
                    pt => pt.TopPartId,
                    tp => tp.Id,
                    (pt, tp) => new WorkflowPartDto 
                    {
                        ProductToPartId = pt.Id,
                        TopPartId = pt.TopPartId,
                        TopPartCode = tp.TopPartCode,
                        TopPartName = tp.TopPartName,
                        QtyPerProduct = pt.QtyPerProduct,
                        Stage = tp.Stage,
                        ParentProductTopPartId = pt.ParentProductTopPartId
                    })
                .OrderBy(x => x.TopPartName)
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
                Connections = connections,
                ProductParts = productParts
            });
        }

        [HttpGet("parts/{versionId}")]
            public async Task<IActionResult> GetParts(int versionId)
            {
                var parts = await _db.ProductTopParts
                    .Where(x => x.VersionId == versionId && x.IsActive)
                    .Join(_db.TopParts.Where(tp => tp.IsActive && tp.Stage == 1),
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

        [HttpGet("productparts/{versionId}")]
            public async Task<IActionResult> GetProductParts(int versionId)
            {
                var parts = await _db.ProductTopParts
                    .Where(x => x.VersionId == versionId && x.IsActive)
                    .Join(_db.TopParts.Where(tp => tp.IsActive && tp.Stage == 1),
                        pt => pt.TopPartId,
                        tp => tp.Id,
                        (pt, tp) => new WorkflowPartDto
                        {
                            ProductToPartId = pt.Id,
                            TopPartId = tp.Id,
                            TopPartCode = tp.TopPartCode,
                            TopPartName = tp.TopPartName,
                            ParentProductTopPartId = pt.ParentProductTopPartId,
                            QtyPerProduct = pt.QtyPerProduct,
                            Stage = tp.Stage
                        })
                    .OrderBy(x => x.Stage)
                    .ThenBy(x => x.TopPartName)
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
                if (dto.ProductToPartId is null)
                    return BadRequest("ProductToPartId nav norādīts.");

                productPart = await _db.ProductTopParts
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.ProductToPartId &&
                        x.VersionId == workflow.VersionId &&
                        x.IsActive);

                if (productPart == null)
                    return BadRequest("ProductTopPart nav atrasts.");
            }

            var maxSort = await _db.WorkflowNodes
                .Where(x => x.WorkflowId == dto.WorkflowId)
                .MaxAsync(x => (int?)x.SortOrder) ?? 0;
            
            if (dto.NodeType == 1)
                {
                    var exists = await _db.WorkflowNodes
                        .AnyAsync(x =>
                            x.WorkflowId == dto.WorkflowId &&
                            x.NodeType == 1 &&
                            x.ProductToPartId == dto.ProductToPartId &&
                            x.IsActive);

                    if (exists)
                        return BadRequest("Šī detaļa Workflow jau ir pievienota.");
                }

            var node = new WorkflowNode
            {
                WorkflowId = dto.WorkflowId,
                NodeType = dto.NodeType,
                Name = dto.Name,
                ProductToPartId = productPart?.Id,
                WorkCenterId = dto.WorkCenterId,
                EstimatedMinutes = dto.EstimatedMinutes,
                Comments = dto.Comments,
                SortOrder = maxSort + 10,
                IsActive = true
            };

            _db.WorkflowNodes.Add(node);

            await _db.SaveChangesAsync();

            Console.WriteLine($"Node ProductToPartId = {node.ProductToPartId}");

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
            
            // FINISH nevar būt nākamais mezgls
            if (fromNode.NodeType == 4)
                return BadRequest("FINISH mezglam nevar pievienot nākamo mezglu.");

            var nextCount = await _db.WorkflowNodeConnections
                .CountAsync(x => x.FromNodeId == dto.FromNodeId);

            if (nextCount > 0)
                return BadRequest("Šim mezglam jau ir pievienots nākamais mezgls.");

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

        [HttpGet("merge/{workflowId}")]
        public async Task<IActionResult> GetMergeNodes(int workflowId)
            {
                var nodes = await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == workflowId &&
                        x.NodeType == 3 &&
                        x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name
                    })
                    .ToListAsync();

                return Ok(nodes);
            }

        [HttpGet("finish/{workflowId}")]
        public async Task<IActionResult> GetFinishNodes(int workflowId)
            {
                var nodes = await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == workflowId &&
                        x.NodeType == 4 &&
                        x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name
                    })
                    .ToListAsync();

                return Ok(nodes);
            }

        [HttpGet("available-topparts")]
            public async Task<IActionResult> GetWorkflowSelect([FromQuery] int versionId)
            {
                var rows = await (
                    from tp in _db.TopParts

                    join ptp in _db.ProductTopParts
                        .Where(x => x.VersionId == versionId && x.IsActive)
                        on tp.Id equals ptp.TopPartId into grp

                    from linked in grp.DefaultIfEmpty()

                    where tp.IsActive
                        && tp.Stage == 1

                    orderby tp.TopPartName

                    select new WorkflowTopPartSelectDto
                    {
                        TopPartId = tp.Id,
                        TopPartName = tp.TopPartName,
                        TopPartCode = tp.TopPartCode,
                        Disabled = linked != null
                    }
                ).ToListAsync();

                return Ok(rows);
            }
        
        [HttpGet("available-subparts")]
            public async Task<IActionResult> GetAvailableSubParts([FromQuery] int versionId)
            {
                var rows = await (
                    from tp in _db.TopParts

                    join ptp in _db.ProductTopParts
                        .Where(x => x.VersionId == versionId && x.IsActive)
                        on tp.Id equals ptp.TopPartId into grp

                    from linked in grp.DefaultIfEmpty()

                    where tp.IsActive
                        && tp.Stage == 1

                    orderby tp.TopPartName

                    select new WorkflowTopPartSelectDto
                    {
                        TopPartId = tp.Id,
                        TopPartName = tp.TopPartName,
                        TopPartCode = tp.TopPartCode,
                        Disabled = false
                    }
                ).ToListAsync();

                return Ok(rows);
            }

        [HttpPost("toppart")]
        public async Task<IActionResult> AddTopPart(AddTopPartRequest dto)
            {
                var exists = await _db.ProductTopParts
                    .AnyAsync(x =>
                        x.VersionId == dto.VersionId &&
                        x.ParentProductTopPartId == dto.ParentProductTopPartId &&
                        x.TopPartId == dto.TopPartId &&
                        x.IsActive);

                if (exists)
                    return Ok();

                _db.ProductTopParts.Add(new ProductTopPart
                {
                    VersionId = dto.VersionId,
                    TopPartId = dto.TopPartId,
                    ParentProductTopPartId = dto.ParentProductTopPartId,
                    QtyPerProduct = 1,
                    SortOrder = 10,
                    IsActive = true
                });

                await _db.SaveChangesAsync();

                return Ok();
            }

    }
}