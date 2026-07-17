using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models; 
using ManiApi.DTOs.WorkFlow;
using ManiApi.Services.Workflow;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly WorkflowValidator _validator;

        public WorkflowController(
            AppDbContext db,
            WorkflowValidator validator)
        {
            _db = db;
            _validator = validator;
        }

        [HttpGet("{versionId}")]
        public async Task<IActionResult> GetWorkflow(int versionId)
        {
            var workflow = await _db.Workflows
            .FirstOrDefaultAsync(x =>
                x.VersionId == versionId &&
                x.IsActive);

Console.WriteLine($"GetWorkflow VersionId = {versionId}");
Console.WriteLine($"Workflow = {(workflow == null ? "NULL" : workflow.Id)}");

        if (workflow == null)
            return NotFound("Workflow nav atrasts.");

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
                        ParentProductTopPartId = pt.ParentProductTopPartId,
                        AttachToNodeId = pt.AttachToNodeId
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
                            QtyPerProduct = pt.QtyPerProduct,
                            ParentProductTopPartId = pt.ParentProductTopPartId,
                            AttachToNodeId = pt.AttachToNodeId,
                            Stage = tp.Stage
                        })
                    .OrderBy(x => x.TopPartName)
                    .ToListAsync();

                return Ok(parts);
            }

        [HttpPost]
        public async Task<IActionResult> CreateWorkflow(CreateWorkflowRequest dto)
        {
            var existing = await _db.Workflows
                .FirstOrDefaultAsync(x =>
                    x.VersionId == dto.VersionId &&
                    x.IsActive);

            if (existing != null)
                return Ok(existing);
            
            var version = await (
                from v in _db.ProductVersions
                join p in _db.Products
                    on v.ProductId equals p.Id
                where v.Id == dto.VersionId
                select new
                {
                    p.ProductName,
                    v.VersionName
                })
                .FirstOrDefaultAsync();

            if (version == null)
                return BadRequest("Versija nav atrasta.");

            var workflowName = $"{version.ProductName} - {version.VersionName}";

            var workflow = new Workflow
            {
                VersionId = dto.VersionId,
                ParentNodeId = null,
                Name = workflowName,
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
                            AttachToNodeId = pt.AttachToNodeId,
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

       [HttpGet("available-flows/{workflowId}")]
        public async Task<IActionResult> GetAvailableFlows(int workflowId)
            {              
                var workflowNodes = await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == workflowId &&
                        x.IsActive)
                    .ToListAsync();

                var workflow = await _db.Workflows
                    .FirstAsync(x =>
                        x.Id == workflowId &&
                        x.IsActive);

                var productParts = await _db.ProductTopParts
                    .Where(x =>
                        x.VersionId == workflow.VersionId &&
                        x.IsActive)
                    .ToListAsync();
                
                var connections = await _db.WorkflowNodeConnections
                    .ToListAsync();
                
                var analyzer = new WorkflowFlowAnalyzer(
                    workflowNodes,
                    connections,
                    productParts);

                return Ok(analyzer.GetAvailableFlows());
            }


        [HttpGet("available-topparts")]
            public async Task<IActionResult> GetWorkflowSelect([FromQuery] int versionId)
            {
                var rows = await (
                    from tp in _db.TopParts

                    join ptp in _db.ProductTopParts
                        .Where(x =>
                            x.VersionId == versionId &&
                            x.IsActive &&
                            x.ParentProductTopPartId == null)
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
                var rows = await _db.TopParts
                    .Where(tp => tp.IsActive && tp.Stage == 1)
                    .OrderBy(tp => tp.TopPartName)
                    .Select(tp => new WorkflowTopPartSelectDto
                    {
                        TopPartId = tp.Id,
                        TopPartName = tp.TopPartName,
                        TopPartCode = tp.TopPartCode,
                        Disabled = false
                    })
                    .ToListAsync();

                return Ok(rows);
            }

        [HttpPost("toppart")]
        public async Task<IActionResult> AddTopPart(AddTopPartRequest dto)
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                var exists = await _db.ProductTopParts
                    .AnyAsync(x =>
                        x.VersionId == dto.VersionId &&
                        x.ParentProductTopPartId == dto.ParentProductTopPartId &&
                        x.TopPartId == dto.TopPartId &&
                        x.IsActive);

                if (exists)
                    return Ok();

                var productPart = new ProductTopPart
                    {
                        VersionId = dto.VersionId,
                        TopPartId = dto.TopPartId,
                        ParentProductTopPartId = dto.ParentProductTopPartId,
                        AttachToNodeId = dto.AttachToNodeId,
                        QtyPerProduct = 1,
                        SortOrder = await _db.ProductTopParts
                            .Where(x => x.VersionId == dto.VersionId)
                            .MaxAsync(x => (int?)x.SortOrder) + 10 ?? 10,
                        IsActive = true
                    };

                    _db.ProductTopParts.Add(productPart);

                    await _db.SaveChangesAsync();

                    var workflow = await _db.Workflows
                        .FirstOrDefaultAsync(x =>
                            x.VersionId == dto.VersionId &&
                            x.IsActive);

                    if (workflow == null)
                        return BadRequest("Workflow nav atrasts.");
                    

                    var topPart = await _db.TopParts
                        .FirstAsync(x => x.Id == dto.TopPartId);

                    var maxSort = await _db.WorkflowNodes
                        .Where(x => x.WorkflowId == workflow.Id)
                        .MaxAsync(x => (int?)x.SortOrder) ?? 0;

                    var partNode = new WorkflowNode
                    {
                        WorkflowId = workflow.Id,
                        NodeType = 1,
                        Name = topPart.TopPartName,
                        ProductToPartId = productPart.Id,
                        SortOrder = maxSort + 10,
                        IsActive = true
                    };

                    _db.WorkflowNodes.Add(partNode);

                    await _db.SaveChangesAsync();

                //     var finishNode = new WorkflowNode
                //     {
                //         WorkflowId = workflow.Id,
                //         NodeType = 4,
                //         Name = "FINISH",
                //         SortOrder = partNode.SortOrder + 10,
                //         IsActive = true
                //     };

                //     _db.WorkflowNodes.Add(finishNode);

                //     await _db.SaveChangesAsync();

                //     _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                //     {
                //         FromNodeId = partNode.Id,
                //         ToNodeId = finishNode.Id
                //     });

                // await _db.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok();
            }

            [HttpPost("subpart")]
                public async Task<IActionResult> AddSubPart(AddTopPartRequest dto)
                {
                    if (dto.ParentProductTopPartId == null)
                        return BadRequest("ParentProductTopPartId nav norādīts.");

                    if (dto.AttachToNodeId == null)
                        return BadRequest("AttachToNodeId nav norādīts.");

                    var parentPart = await _db.ProductTopParts
                        .FirstOrDefaultAsync(x =>
                            x.Id == dto.ParentProductTopPartId &&
                            x.VersionId == dto.VersionId &&
                            x.IsActive);

                    if (parentPart == null)
                        return BadRequest("Parent ProductTopPart nav atrasts.");

                    var attachNode = await _db.WorkflowNodes
                        .FirstOrDefaultAsync(x =>
                            x.Id == dto.AttachToNodeId &&
                            x.IsActive);

                    if (attachNode == null)
                        return BadRequest("Attach PART mezgls nav atrasts.");

                    return await AddTopPart(dto);
                }

            [HttpPost("process")]
            public async Task<IActionResult> AddProcess(AddProcessRequest dto)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowId &&
                        x.IsActive);

                if (workflow == null)
                    return BadRequest("Workflow nav atrasts.");
                
                

                var previousNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.PreviousNodeId &&
                        x.IsActive);

                
                if (previousNode == null)
                    return BadRequest("Previous node nav atrasts.");
                
                if (previousNode.WorkflowId != workflow.Id)
                    return BadRequest("Mezgls nepieder šim Workflow.");

                var connections = await _db.WorkflowNodeConnections
                    .Where(x => x.FromNodeId == previousNode.Id)
                    .ToListAsync();

                if (connections.Count > 1)
                    return BadRequest("Aktīvajam mezglam ir vairāk nekā viens nākamais mezgls.");

                WorkflowNodeConnection? oldConnection = connections.FirstOrDefault();

                WorkflowNode? nextNode = null;

                if (oldConnection != null)
                {
                    nextNode = await _db.WorkflowNodes
                        .FirstOrDefaultAsync(x =>
                            x.Id == oldConnection.ToNodeId &&
                            x.IsActive);

                    if (nextNode == null)
                        return BadRequest("Nākamais mezgls nav atrasts.");
                }
                
                // if (nextNode.NodeType != 4)
                //     return BadRequest("Nākamajam mezglam jābūt FINISH.");

                var finishNode = nextNode;
                
                var processNode = new WorkflowNode
                {
                    WorkflowId = workflow.Id,
                    NodeType = 2,
                    Name = dto.ProcessName,
                    SortOrder = previousNode.SortOrder + 10,
                    IsActive = true
                };

                _db.WorkflowNodes.Add(processNode);

                await _db.SaveChangesAsync();

                if (nextNode?.NodeType == 4)
                    {
                        nextNode.SortOrder += 10;
                        _db.WorkflowNodes.Update(nextNode);
                    }

                if (oldConnection != null)
                    {
                        _db.WorkflowNodeConnections.Remove(oldConnection);
                    }

                _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                {
                    FromNodeId = previousNode.Id,
                    ToNodeId = processNode.Id
                });

                if (nextNode != null)
                {
                    _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                    {
                        FromNodeId = processNode.Id,
                        ToNodeId = nextNode.Id
                    });
                }               

                await _db.SaveChangesAsync();

                return Ok(processNode);

            }

            [HttpPost("merge")]
            public async Task<IActionResult> AddMerge(AddMergeRequest dto)
            {
                await using var transaction = await _db.Database.BeginTransactionAsync();

                var workflowNodes = await _db.WorkflowNodes
                    .Where(x => x.WorkflowId == dto.WorkflowId && x.IsActive)
                    .ToListAsync();

                var connections = await _db.WorkflowNodeConnections
                    .Where(x => workflowNodes.Select(n => n.Id).Contains(x.FromNodeId))
                    .ToListAsync();

                var productParts = await _db.ProductTopParts
                    .Where(x => x.IsActive)
                    .ToListAsync();

                var analyzer = new WorkflowFlowAnalyzer(
                    workflowNodes,
                    connections,
                    productParts);

                var finishNodeIds = dto.MergeFinishNodeIds
                    .Append(dto.CurrentFinishNodeId)
                    .ToList();

                if (finishNodeIds.Count != finishNodeIds.Distinct().Count())
                    return BadRequest("Tas pats Finished Flow izvēlēts vairākas reizes.");

                finishNodeIds = finishNodeIds
                    .Distinct()
                    .ToList();

                if (finishNodeIds.Count < 2)
                    return BadRequest("MERGE nepieciešami vismaz divi Finished Flow.");

                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowId &&
                        x.IsActive);

                if (workflow == null)
                    return BadRequest("Workflow nav atrasts.");
                
                var previousNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.CurrentFinishNodeId &&
                        x.WorkflowId == workflow.Id &&
                        x.NodeType == 4 &&
                        x.IsActive);

                if (previousNode == null)
                    return BadRequest("FINISH mezgls nav atrasts.");

                if (previousNode == null)
                    return BadRequest("PART FINISH nav atrasts.");
                
                
                var maxSort = await _db.WorkflowNodes
                    .Where(x => x.WorkflowId == workflow.Id)
                    .MaxAsync(x => (int?)x.SortOrder) ?? 0;

                var mergeNode = new WorkflowNode
                {
                    WorkflowId = workflow.Id,
                    NodeType = 3,
                    Name = "MERGE",
                    SortOrder = maxSort + 10,
                    IsActive = true
                };

                 _db.WorkflowNodes.Add(mergeNode);

                await _db.SaveChangesAsync();

Console.WriteLine($"FINISH COUNT = {finishNodeIds.Count}");

                foreach (var finishId in finishNodeIds)
                {
Console.WriteLine($"CONNECTING FINISH {finishId}");
                    var finishNode = await _db.WorkflowNodes
                        .FirstOrDefaultAsync(x =>
                            x.Id == finishId &&
                            x.WorkflowId == workflow.Id &&
                            x.NodeType == 4 &&
                            x.IsActive);

                    if (finishNode == null)
                        return BadRequest($"FINISH mezgls {finishId} nav atrasts.");
                    
                    // var previousFinishNode = await _db.WorkflowNodeConnections
                    //     .Where(x => x.ToNodeId == finishNode.Id)
                    //     .Join(_db.WorkflowNodes,
                    //         c => c.FromNodeId,
                    //         n => n.Id,
                    //         (c, n) => n)
                    //     .FirstOrDefaultAsync();

                    // if (previousFinishNode == null)
                    //     return BadRequest("FINISH nav iepriekšējā mezgla.");

                    if (finishNode.Id != dto.CurrentFinishNodeId &&
                        finishNodeIds.Count(x => x == finishNode.Id) > 1)
                    {
                        return BadRequest("Tas pats Finished Flow izvēlēts vairākas reizes.");
                    }
                                        
                    var mergeConnectionExists = await _db.WorkflowNodeConnections
                        .AnyAsync(x => x.FromNodeId == finishId);

                    if (mergeConnectionExists)
                        return BadRequest("Selected Flow jau ir izmantots citā MERGE.");
                    
                    _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                    {
                        FromNodeId = finishNode.Id,
                        ToNodeId = mergeNode.Id
                    });
                }

                

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(mergeNode);
            }

            [HttpPost("validate/{workflowId}")]
            public async Task<IActionResult> ValidateWorkflow(int workflowId)
            {
                var result = await _validator.ValidateAsync(workflowId);

                return Ok(result);
            }

            [HttpPost("node/comments")]
            public async Task<IActionResult> SaveNodeComments(SaveNodeCommentsRequest dto)
            {
                var node = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x => x.Id == dto.NodeId && x.IsActive);

                if (node == null)
                    return NotFound();

                node.Comments = dto.Comments;

                await _db.SaveChangesAsync();

                return Ok();
            }

            [HttpPost("process/save")]
            public async Task<IActionResult> SaveProcess(
                SaveProcessRequest dto)
                {
                    var node = await _db.WorkflowNodes
                        .FirstOrDefaultAsync(x =>
                            x.Id == dto.NodeId &&
                            x.IsActive);

                    if (node == null)
                        return NotFound();

                    node.Name = dto.Name;
                    node.WorkCenterId = dto.WorkCenterId;
                    node.EstimatedMinutes = dto.EstimatedMinutes;
                    node.Comments = dto.Comments;

                    await _db.SaveChangesAsync();

                    return Ok();
                }

            [HttpPost("part/qty")]
            public async Task<IActionResult> SaveQtyPerProduct(SaveQtyPerProductRequest dto)
            {
                var part = await _db.ProductTopParts
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.ProductToPartId &&
                        x.IsActive);

                if (part == null)
                    return NotFound();

                part.QtyPerProduct = dto.QtyPerProduct < 1
                    ? 1
                    : dto.QtyPerProduct;

                await _db.SaveChangesAsync();

                return Ok();
            }

            [HttpPost("finish")]
            public async Task<IActionResult> AddFinish(AddFinishRequest dto)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowId &&
                        x.IsActive);

                if (workflow == null)
                    return BadRequest("Workflow nav atrasts.");

                var flowOwner = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.FlowOwnerNodeId &&
                        x.WorkflowId == workflow.Id &&
                        x.IsActive);

                if (flowOwner == null)
                    return BadRequest("Flow sākuma mezgls nav atrasts.");

                var workflowNodes = await _db.WorkflowNodes
                    .Where(x => x.WorkflowId == workflow.Id && x.IsActive)
                    .ToListAsync();

                var connections = await _db.WorkflowNodeConnections
                    .Where(x => workflowNodes.Select(n => n.Id).Contains(x.FromNodeId))
                    .ToListAsync();

                var productParts = await _db.ProductTopParts
                    .Where(x => x.IsActive)
                    .ToListAsync();

                var analyzer = new WorkflowFlowAnalyzer(
                    workflowNodes,
                    connections,
                    productParts);
                
                if (flowOwner.NodeType != 1 &&
                        flowOwner.NodeType != 3)
                    {
                        return BadRequest("Flow Owner drīkst būt tikai PART vai MERGE mezgls.");
                    }
                
                var finishNode = analyzer.GetFlowFinishNodeByOwner(
                    flowOwner.ProductToPartId ?? 0);

                if (finishNode != null)
                    return BadRequest("Šai plūsmai FINISH jau eksistē.");

                var lastNode = flowOwner;

                while (true)
                {
                    var connection = connections
                        .FirstOrDefault(x => x.FromNodeId == lastNode.Id);

                    if (connection == null)
                        break;

                    var next = workflowNodes
                        .First(x => x.Id == connection.ToNodeId);

                    lastNode = next;
                }

                var newFinish = new WorkflowNode
                {
                    WorkflowId = workflow.Id,
                    NodeType = 4,
                    Name = "FINISH",
                    SortOrder = lastNode.SortOrder + 10,
                    IsActive = true
                };

                _db.WorkflowNodes.Add(newFinish);

                await _db.SaveChangesAsync();

                _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                {
                    FromNodeId = lastNode.Id,
                    ToNodeId = newFinish.Id
                });

                await _db.SaveChangesAsync();

                return Ok(newFinish);
            }

    }
}