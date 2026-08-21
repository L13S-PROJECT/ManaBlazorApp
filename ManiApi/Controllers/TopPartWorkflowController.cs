using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;
using ManiApi.DTOs.WorkFlow;
using ManaApp.Shared.DTOs.TopPart;
using ManiApi.Services.TopParts;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopPartWorkflowController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TopPartWorkflowService _workflowService;

        private readonly TopPartWorkflowGraphService _graphService;

        public TopPartWorkflowController(
            AppDbContext db,
            TopPartWorkflowService workflowService,
            TopPartWorkflowGraphService graphService)
        {
            _db = db;
            _workflowService = workflowService;
            _graphService = graphService;
        }

        [HttpGet("toppart/{topPartId}/versions")]
            public async Task<IActionResult> GetWorkflowVersions(int topPartId)
            {
                var workflows = await _db.Workflows
                    .Where(x =>
                        x.TopPartId == topPartId &&
                        x.IsActive)
                    .OrderByDescending(x => x.WorkflowVersion)
                    .Select(x => new
                    {
                        x.Id,
                        x.TopPartId,
                        x.WorkflowVersion,
                        x.Status,
                        x.IsCurrent,
                        x.Name
                    })
                    .ToListAsync();

                return Ok(workflows);
            }

        [HttpPost]
            public async Task<IActionResult> CreateWorkflow(CreateTopPartWorkflowRequest dto)
            {
                
                await using var transaction =
                        await _db.Database.BeginTransactionAsync();

                var topPart = await _db.TopParts
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.TopPartId &&
                        x.IsActive);

                if (topPart == null)
                    return BadRequest("TopPart nav atrasts.");

                var workflowExists = await _db.Workflows
                    .AnyAsync(x =>
                        x.TopPartId == dto.TopPartId &&
                        x.IsActive);

                if (workflowExists)
                    return BadRequest(
                        "TopPart Workflow jau eksistē. Jauna versija jāveido no esošā Workflow.");

                var nextVersion = await _db.Workflows
                    .Where(x => x.TopPartId == dto.TopPartId)
                    .MaxAsync(x => (int?)x.WorkflowVersion) ?? 0;

                nextVersion++;

                var workflow = new Workflow
                    {
                        TopPartId = dto.TopPartId,
                        WorkflowVersion = nextVersion,
                        Status = WorkflowStatus.Draft,
                        VersionId = null,
                        ParentNodeId = null,
                        Name = $"{topPart.TopPartName} - V{nextVersion}",
                        CreatedDate = DateTime.Now,
                        IsCurrent = false,
                        IsActive = true
                    };

                _db.Workflows.Add(workflow);

                await _db.SaveChangesAsync();

                var partNode = new WorkflowNode
                    {
                        WorkflowId = workflow.Id,
                        NodeType = (byte)WorkflowNodeType.Part,
                        Name = topPart.TopPartName,
                        TopPartId = dto.TopPartId,
                        SortOrder = 10,
                        IsActive = true
                    };

                _db.WorkflowNodes.Add(partNode);

                await _db.SaveChangesAsync();

                var finishNode = new WorkflowNode
                    {
                        WorkflowId = workflow.Id,
                        NodeType = (byte)WorkflowNodeType.Finish,
                        Name = "FINISH",
                        TopPartId = dto.TopPartId,
                        SortOrder = 20,
                        IsActive = true
                    };

                _db.WorkflowNodes.Add(finishNode);

                await _db.SaveChangesAsync();

                // var connection = new WorkflowNodeConnection
                //     {
                //         FromNodeId = partNode.Id,
                //         ToNodeId = finishNode.Id
                //     };

                // _db.WorkflowNodeConnections.Add(connection);

                // await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                    {
                        workflow.Id,
                        workflow.TopPartId,
                        workflow.WorkflowVersion,
                        workflow.Status,
                        workflow.IsCurrent,
                        workflow.Name
                    });
            }

        [HttpGet("{workflowId}")]
        public async Task<IActionResult> GetWorkflow(int workflowId)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(x =>
                    x.Id == workflowId &&
                    x.TopPartId != null &&
                    x.IsActive);

            if (workflow == null)
                return NotFound("TopPart Workflow nav atrasts.");
            
            var topPartName = await _db.TopParts
                .Where(x => x.Id == workflow.TopPartId)
                .Select(x => x.TopPartName)
                .FirstOrDefaultAsync() ?? string.Empty;

            var nodes = await _db.WorkflowNodes
                .Where(x =>
                    x.WorkflowId == workflow.Id &&
                    x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            var nodeIds = nodes
                .Select(x => x.Id)
                .ToList();

            var connections = await _db.WorkflowNodeConnections
                .Where(x =>
                    nodeIds.Contains(x.FromNodeId) &&
                    nodeIds.Contains(x.ToNodeId))
                .ToListAsync();
            
            var nodeDtos = nodes.Select(node =>
                {
                    var dto = new TopPartWorkflowNodeDto
                    {
                        Id = node.Id,
                        WorkflowId = node.WorkflowId,
                        NodeType = node.NodeType,
                        Name = node.Name,
                        TopPartId = node.TopPartId,
                        WorkCenterId = node.WorkCenterId,
                        WorkCenterName = node.WorkCenterId.HasValue
                            ? _db.WorkCenters
                                .Where(x => x.Id == node.WorkCenterId.Value)
                                .Select(x => x.WorkCentr_Name)
                                .FirstOrDefault()
                            : null,
                        StepTypeId = node.StepTypeId,

                        StepTypeName = node.StepTypeId.HasValue
                            ? _db.StepTypes
                                .Where(x => x.Id == node.StepTypeId.Value)
                                .Select(x => x.StepTypeName)
                                .FirstOrDefault()
                            : null,
                        EstimatedMinutes = node.EstimatedMinutes,
                        Comments = node.Comments,
                        SortOrder = node.SortOrder
                    };

                    return dto;
                }).ToList();

            var connectionDtos = connections
                .Select(x => new TopPartWorkflowConnectionDto
                {
                    Id = x.Id,
                    FromNodeId = x.FromNodeId,
                    ToNodeId = x.ToNodeId
                })
                .ToList();

            foreach (var processDto in nodeDtos
                    .Where(x => x.NodeType == (byte)WorkflowNodeType.Process))
                {
                    var connection = connections
                        .FirstOrDefault(x => x.FromNodeId == processDto.Id);

                    if (connection == null)
                        continue;

                    var wipNode = nodes.FirstOrDefault(x =>
                        x.Id == connection.ToNodeId &&
                        x.NodeType == (byte)WorkflowNodeType.Wip);

                    if (wipNode == null)
                        continue;

                    processDto.OutputWipNodeId = wipNode.Id;
                    processDto.OutputWipName = wipNode.Name;
                }

            _graphService.CalculateLayout(nodeDtos, connectionDtos);
            
            var components = await _db.WorkflowComponents
                .Where(x =>
                    x.WorkflowId == workflow.Id &&
                    x.IsActive)
                .ToListAsync();
            
            var processComponents = await _db.WorkflowProcessComponents
                .Where(x => nodeIds.Contains(x.ProcessNodeId))
                .ToListAsync();

            return Ok(new
                {
                    TopPartName = topPartName,
                    Workflow = new
                    {
                        workflow.Id,
                        workflow.TopPartId,
                        workflow.WorkflowVersion,
                        workflow.Status,
                        workflow.IsCurrent,
                        workflow.Name,
                        workflow.Description
                    },
                    Nodes = nodeDtos
                        .OrderBy(x => x.GraphLevel)
                        .ThenBy(x => x.GraphColumn)
                        .ToList(),
                    Connections = connections,
                    Components = components,
                    ProcessComponents = processComponents
                });
        }

        [HttpPost("process")]
            public async Task<IActionResult> AddProcess(CreateTopPartProcessRequest dto)
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (workflow == null)
                    return BadRequest("TopPart Workflow nav atrasts.");
                
                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");

                if (string.IsNullOrWhiteSpace(dto.WipName))
                    return BadRequest("WIP nosaukums ir obligāts.");
                
                if (dto.SelectedNodeIds == null || dto.SelectedNodeIds.Count == 0)
                    return BadRequest("PROCESS nepieciešams vismaz viens ieejas mezgls.");
                
                var selectedNodes = await _db.WorkflowNodes
                    .Where(x =>
                        dto.SelectedNodeIds.Contains(x.Id) &&
                        x.WorkflowId == workflow.Id &&
                        x.IsActive)
                    .ToListAsync();

                if (selectedNodes.Count != dto.SelectedNodeIds.Distinct().Count())
                    return BadRequest("Viens vai vairāki izvēlētie mezgli nav atrasti šajā Workflow.");
                
                if (selectedNodes.Any(x =>
                    x.NodeType != (byte)WorkflowNodeType.Part &&
                    x.NodeType != (byte)WorkflowNodeType.Wip))
                {
                    return BadRequest("PROCESS ieejas drīkst būt tikai PART vai WIP mezgli.");
                }

                var isMerge = selectedNodes.Count > 1;

                if (isMerge &&
                        selectedNodes.Any(x => x.NodeType != (byte)WorkflowNodeType.Wip))
                    {
                        return BadRequest("MERGE PROCESS ieejas drīkst būt tikai WIP mezgli.");
                    }
                
                var selectedWipIds = selectedNodes
                    .Where(x => x.NodeType == (byte)WorkflowNodeType.Wip)
                    .Select(x => x.Id)
                    .ToList();

                var alreadyConsumedWip = await (
                    from connection in _db.WorkflowNodeConnections
                    join node in _db.WorkflowNodes
                        on connection.ToNodeId equals node.Id
                    where selectedWipIds.Contains(connection.FromNodeId)
                        && node.WorkflowId == workflow.Id
                        && node.NodeType == (byte)WorkflowNodeType.Process
                        && node.IsActive
                    select connection
                ).AnyAsync();

                if (alreadyConsumedWip)
                {
                    return BadRequest(
                        "Izvēlētais WIP jau ir patērēts citā PROCESS.");
                }
            

                var existingOutgoingConnections = await _db.WorkflowNodeConnections
                    .Where(x => dto.SelectedNodeIds.Contains(x.FromNodeId))
                    .ToListAsync();

                var mergeNextNodeIds = isMerge
                    ? existingOutgoingConnections
                        .Select(x => x.ToNodeId)
                        .Distinct()
                        .ToList()
                    : new List<int>();

                if (isMerge && mergeNextNodeIds.Count > 1)
                    {
                        return BadRequest(
                            "MERGE PROCESS izvēlētajiem WIP jābūt vienam kopīgam nākamajam mezglam.");
                    }

                var consumedWipConnections = isMerge
                    ? existingOutgoingConnections
                    : new List<WorkflowNodeConnection>();

                var sortAnchorNode = selectedNodes
                    .OrderByDescending(x => x.SortOrder)
                    .First();
                


                await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == workflow.Id &&
                        x.SortOrder > sortAnchorNode.SortOrder &&
                        x.IsActive)
                    .ExecuteUpdateAsync(x =>
                        x.SetProperty(n => n.SortOrder, n => n.SortOrder + 20));

                var processNode = new WorkflowNode
                    {
                        WorkflowId = workflow.Id,
                        NodeType = (byte)WorkflowNodeType.Process,
                        Name = dto.ProcessName,
                        TopPartId = workflow.TopPartId,
                        WorkCenterId = dto.WorkCenterId,
                        StepTypeId = dto.StepTypeId,
                        EstimatedMinutes = dto.EstimatedMinutes,
                        SortOrder = sortAnchorNode.SortOrder + 10,
                        IsActive = true
                    };

                _db.WorkflowNodes.Add(processNode);

                await _db.SaveChangesAsync();

                var wipNode = new WorkflowNode
                    {
                        WorkflowId = workflow.Id,
                        NodeType = (byte)WorkflowNodeType.Wip,
                        Name = dto.WipName.Trim(),
                        TopPartId = workflow.TopPartId,
                        SortOrder = sortAnchorNode.SortOrder + 20,
                        IsActive = true
                    };

                _db.WorkflowNodes.Add(wipNode);

                await _db.SaveChangesAsync();

                var inputConnections = selectedNodes
                    .Select(x => new WorkflowNodeConnection
                    {
                        FromNodeId = x.Id,
                        ToNodeId = processNode.Id
                    })
                    .ToList();
            

                if (isMerge && consumedWipConnections.Count > 0)
                    {
                        _db.WorkflowNodeConnections.RemoveRange(consumedWipConnections);
                    }


                if (!isMerge)
                    {
                        var finishNodeId = await _db.WorkflowNodes
                            .Where(x =>
                                x.WorkflowId == workflow.Id &&
                                x.NodeType == (byte)WorkflowNodeType.Finish &&
                                x.IsActive)
                            .Select(x => x.Id)
                            .FirstOrDefaultAsync();

                        if (finishNodeId != 0)
                        {
                            var connectionToFinish = existingOutgoingConnections
                                .Where(x => x.ToNodeId == finishNodeId)
                                .ToList();

                            if (connectionToFinish.Count > 0)
                                _db.WorkflowNodeConnections.RemoveRange(connectionToFinish);
                        }
                    }

                _db.WorkflowNodeConnections.AddRange(inputConnections);

                _db.WorkflowNodeConnections.AddRange(

                    new WorkflowNodeConnection
                        {
                            FromNodeId = processNode.Id,
                            ToNodeId = wipNode.Id
                        });

                if (isMerge)
                    {
                        foreach (var nextNodeId in mergeNextNodeIds)
                        {
                            _db.WorkflowNodeConnections.Add(
                                new WorkflowNodeConnection
                                {
                                    FromNodeId = wipNode.Id,
                                    ToNodeId = nextNodeId
                                });
                        }
                    }


                await _db.SaveChangesAsync();

                await _workflowService.UpdateFinishConnectionAsync(workflow.Id);

                await transaction.CommitAsync();

                return Ok(processNode);
            }

        [HttpPost("finish")]
        public async Task<IActionResult> AddFinish(AddTopPartFinishRequest dto)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(x =>
                    x.Id == dto.WorkflowId &&
                    x.TopPartId != null &&
                    x.IsActive);

            if (workflow == null)
                return BadRequest("TopPart Workflow nav atrasts.");

            if (workflow.Status != WorkflowStatus.Draft)
                return BadRequest("RELEASED Workflow modificēt nedrīkst.");

            var finishNode = await _db.WorkflowNodes
                .FirstOrDefaultAsync(x =>
                    x.WorkflowId == workflow.Id &&
                    x.NodeType == (byte)WorkflowNodeType.Finish &&
                    x.IsActive);

            if (finishNode == null)
                return BadRequest("FINISH mezgls nav atrasts.");

            var wipNodes = await _db.WorkflowNodes
                .Where(x =>
                    x.WorkflowId == workflow.Id &&
                    x.NodeType == (byte)WorkflowNodeType.Wip &&
                    x.IsActive)
                .ToListAsync();

            var connections = await _db.WorkflowNodeConnections
                .Where(x =>
                    wipNodes.Select(w => w.Id).Contains(x.FromNodeId))
                .ToListAsync();

            var freeWips = wipNodes
                .Where(wip =>
                    !connections.Any(x => x.FromNodeId == wip.Id))
                .ToList();

            if (freeWips.Count != 1)
                return BadRequest("FINISH drīkst pievienot tikai tad, ja ir tieši viens brīvs WIP.");

            if (freeWips[0].Id != dto.WipNodeId)
                return BadRequest("Izvēlētais WIP nav vienīgais brīvais WIP.");

            var alreadyConnected = await _db.WorkflowNodeConnections
                .AnyAsync(x =>
                    x.FromNodeId == dto.WipNodeId &&
                    x.ToNodeId == finishNode.Id);

            if (alreadyConnected)
                return BadRequest("FINISH jau ir pievienots šim WIP.");

            _db.WorkflowNodeConnections.Add(
                new WorkflowNodeConnection
                {
                    FromNodeId = dto.WipNodeId,
                    ToNodeId = finishNode.Id
                });

            await _db.SaveChangesAsync();

            return Ok();
        }

        [HttpPut("process")]
            public async Task<IActionResult> UpdateProcess(UpdateTopPartProcessRequest dto)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowId &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("Workflow nav atrasts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");
                
                if (string.IsNullOrWhiteSpace(dto.WipName))
                    return BadRequest("WIP nosaukums ir obligāts.");
                
                var processNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.ProcessNodeId &&
                        x.WorkflowId == dto.WorkflowId &&
                        x.NodeType == (byte)WorkflowNodeType.Process &&
                        x.IsActive);

                if (processNode == null)
                    return NotFound("PROCESS mezgls nav atrasts.");

                var wipNode = await (
                    from connection in _db.WorkflowNodeConnections
                    join node in _db.WorkflowNodes
                        on connection.ToNodeId equals node.Id
                    where connection.FromNodeId == processNode.Id
                        && node.WorkflowId == dto.WorkflowId
                        && node.NodeType == (byte)WorkflowNodeType.Wip
                        && node.IsActive
                    select node
                ).FirstOrDefaultAsync();

                if (wipNode == null)
                    return BadRequest("PROCESS WIP mezgls nav atrasts.");

                processNode.Name = dto.ProcessName;
                processNode.WorkCenterId = dto.WorkCenterId;
                processNode.StepTypeId = dto.StepTypeId;
                processNode.EstimatedMinutes = dto.EstimatedMinutes;
                wipNode.Name = dto.WipName.Trim();

                await _db.SaveChangesAsync();

                return Ok(processNode);
            }

        [HttpDelete("process/{workflowId}/{processNodeId}")]
                public async Task<IActionResult> DeleteProcess(
                    int workflowId,
                    int processNodeId)
                {
                    await using var transaction =
                        await _db.Database.BeginTransactionAsync();

                    var workflow = await _db.Workflows
                        .FirstOrDefaultAsync(x =>
                            x.Id == workflowId &&
                            x.IsActive);

                    if (workflow == null)
                        return NotFound("Workflow nav atrasts.");

                    if (workflow.Status != WorkflowStatus.Draft)
                        return BadRequest("RELEASED Workflow modificēt nedrīkst.");
                    
                    var processNode = await _db.WorkflowNodes
                        .FirstOrDefaultAsync(x =>
                            x.Id == processNodeId &&
                            x.WorkflowId == workflowId &&
                            x.NodeType == (byte)WorkflowNodeType.Process &&
                            x.IsActive);

                    if (processNode == null)
                        return NotFound("PROCESS mezgls nav atrasts.");
                    
                    var incomingConnections = await _db.WorkflowNodeConnections
                        .Where(x => x.ToNodeId == processNode.Id)
                        .ToListAsync();

                    if (incomingConnections.Count == 0)
                        return BadRequest("PROCESS mezglam nav iepriekšējā savienojuma.");
                    
                    var outgoingConnection = await _db.WorkflowNodeConnections
                        .FirstOrDefaultAsync(x =>
                            x.FromNodeId == processNode.Id);

                    if (outgoingConnection == null)
                        return BadRequest("PROCESS mezglam nav nākamā savienojuma.");
                    
                    var wipNode = await _db.WorkflowNodes
                        .FirstOrDefaultAsync(x =>
                            x.Id == outgoingConnection.ToNodeId &&
                            x.WorkflowId == workflowId &&
                            x.NodeType == (byte)WorkflowNodeType.Wip &&
                            x.IsActive);

                    if (wipNode == null)
                        return BadRequest("PROCESS WIP mezgls nav atrasts.");

                    var wipOutgoingConnection = await _db.WorkflowNodeConnections
                        .FirstOrDefaultAsync(x =>
                            x.FromNodeId == wipNode.Id);

                    if (wipOutgoingConnection != null)
                        {
                            var nextNode = await _db.WorkflowNodes
                                .FirstOrDefaultAsync(x =>
                                    x.Id == wipOutgoingConnection.ToNodeId &&
                                    x.WorkflowId == workflowId &&
                                    x.IsActive);

                            if (nextNode?.NodeType == (byte)WorkflowNodeType.Process)
                                {
                                    return BadRequest(
                                        "PROCESS nevar dzēst, jo tā WIP tiek izmantots nākamajā PROCESS. Vispirms jāizdzēš nākamais PROCESS.");
                                }
                        }

                    if (wipOutgoingConnection != null && incomingConnections.Count == 1)
                        {
                            _db.WorkflowNodeConnections.Add(
                                new WorkflowNodeConnection
                                {
                                    FromNodeId = incomingConnections[0].FromNodeId,
                                    ToNodeId = wipOutgoingConnection.ToNodeId
                                });
                        }

                    _db.WorkflowNodes.Remove(processNode);
                    _db.WorkflowNodes.Remove(wipNode);  

                    await _db.SaveChangesAsync();

                    await _db.WorkflowNodes
                        .Where(x =>
                            x.WorkflowId == workflowId &&
                            x.SortOrder > processNode.SortOrder &&
                            x.IsActive)
                        .ExecuteUpdateAsync(x =>
                            x.SetProperty(n => n.SortOrder, n => n.SortOrder - 20));

                    await _workflowService.UpdateFinishConnectionAsync(workflowId);

                    await transaction.CommitAsync();

                    return NoContent();
                }

        [HttpPost("{workflowId}/process/component")]
            public async Task<IActionResult> AddProcessComponent(
                int workflowId,
                AddTopPartProcessComponentRequest dto)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("Workflow nav atrasts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");

                if (dto.Quantity <= 0)
                    return BadRequest("Daudzumam jābūt lielākam par 0.");

                var processNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.ProcessNodeId &&
                        x.WorkflowId == workflowId &&
                        x.NodeType == (byte)WorkflowNodeType.Process &&
                        x.IsActive);

                if (processNode == null)
                    return BadRequest("PROCESS nav atrasts šajā Workflow.");

                var component = await _db.WorkflowComponents
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowComponentId &&
                        x.WorkflowId == workflowId &&
                        x.IsActive);

                if (component == null)
                    return BadRequest("Komponente nav atrasta šajā Workflow.");

                var alreadyAdded = await _db.WorkflowProcessComponents
                    .AnyAsync(x =>
                        x.ProcessNodeId == dto.ProcessNodeId &&
                        x.WorkflowComponentId == dto.WorkflowComponentId);

                if (alreadyAdded)
                    return BadRequest("Šī BOM komponente šim PROCESS jau ir pievienota.");

                var usedQuantity = await _db.WorkflowProcessComponents
                    .Where(x => x.WorkflowComponentId == component.Id)
                    .SumAsync(x => (decimal?)x.Quantity) ?? 0;

                if (usedQuantity + dto.Quantity > component.Quantity)
                    return BadRequest("Pievienotais daudzums pārsniedz BOM atlikumu.");

                var processComponent = new WorkflowProcessComponent
                {
                    ProcessNodeId = processNode.Id,
                    WorkflowComponentId = component.Id,
                    Quantity = dto.Quantity
                };

                _db.WorkflowProcessComponents.Add(processComponent);
                await _db.SaveChangesAsync();

                return Ok(processComponent);
            }

        [HttpPut("{workflowId}/process/component")]
         public async Task<IActionResult> UpdateProcessComponent(
             int workflowId,
                UpdateTopPartProcessComponentRequest dto)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("Workflow nav atrasts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");

                if (dto.Quantity <= 0)
                    return BadRequest("Daudzumam jābūt lielākam par 0.");

                var processNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.ProcessNodeId &&
                        x.WorkflowId == workflowId &&
                        x.NodeType == (byte)WorkflowNodeType.Process &&
                        x.IsActive);

                if (processNode == null)
                    return BadRequest("PROCESS nav atrasts šajā Workflow.");

                var component = await _db.WorkflowComponents
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.WorkflowComponentId &&
                        x.WorkflowId == workflowId &&
                        x.IsActive);

                if (component == null)
                    return BadRequest("Komponente nav atrasta šajā Workflow.");

                var processComponent = await _db.WorkflowProcessComponents
                    .FirstOrDefaultAsync(x =>
                        x.ProcessNodeId == dto.ProcessNodeId &&
                        x.WorkflowComponentId == dto.WorkflowComponentId);

                if (processComponent == null)
                    return NotFound("PROCESS komponentes sasaiste nav atrasta.");

                var usedByOtherProcesses = await _db.WorkflowProcessComponents
                    .Where(x =>
                        x.WorkflowComponentId == dto.WorkflowComponentId &&
                        x.ProcessNodeId != dto.ProcessNodeId)
                    .SumAsync(x => (decimal?)x.Quantity) ?? 0;

                if (usedByOtherProcesses + dto.Quantity > component.Quantity)
                    return BadRequest("Norādītais daudzums pārsniedz BOM atlikumu.");

                processComponent.Quantity = dto.Quantity;

                await _db.SaveChangesAsync();

                return Ok(processComponent);
            }

        [HttpDelete("{workflowId}/process/component/{processNodeId}/{workflowComponentId}")]
            public async Task<IActionResult> DeleteProcessComponent(
                int workflowId,
                int processNodeId,
                int workflowComponentId)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("Workflow nav atrasts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");

                var processNodeExists = await _db.WorkflowNodes
                    .AnyAsync(x =>
                        x.Id == processNodeId &&
                        x.WorkflowId == workflowId &&
                        x.NodeType == (byte)WorkflowNodeType.Process &&
                        x.IsActive);

                if (!processNodeExists)
                    return BadRequest("PROCESS nav atrasts šajā Workflow.");

                var componentExists = await _db.WorkflowComponents
                    .AnyAsync(x =>
                        x.Id == workflowComponentId &&
                        x.WorkflowId == workflowId &&
                        x.IsActive);

                if (!componentExists)
                    return BadRequest("Komponente nav atrasta šajā Workflow.");

                var processComponent = await _db.WorkflowProcessComponents
                    .FirstOrDefaultAsync(x =>
                        x.ProcessNodeId == processNodeId &&
                        x.WorkflowComponentId == workflowComponentId);

                if (processComponent == null)
                    return NotFound("PROCESS komponentes sasaiste nav atrasta.");

                _db.WorkflowProcessComponents.Remove(processComponent);
                await _db.SaveChangesAsync();

                return NoContent();
            }

        [HttpGet("{workflowId}/draft")]
        public async Task<IActionResult> GetWorkflowDraft(int workflowId)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(x =>
                    x.Id == workflowId &&
                    x.TopPartId != null &&
                    x.IsActive);

            if (workflow == null)
                return NotFound("TopPart Workflow nav atrasts.");
            
            if (workflow.Status != WorkflowStatus.Draft)
                return BadRequest("Šis Workflow nav DRAFT.");

            var nodes = await _db.WorkflowNodes
                .Where(x =>
                    x.WorkflowId == workflow.Id &&
                    x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            
            var nodeIds = nodes
                .Select(x => x.Id)
                .ToList();

            var connections = await _db.WorkflowNodeConnections
                .Where(x =>
                    nodeIds.Contains(x.FromNodeId) &&
                    nodeIds.Contains(x.ToNodeId))
                .ToListAsync();
            
            var components = await _db.WorkflowComponents
                .Where(x =>
                    x.WorkflowId == workflow.Id &&
                    x.IsActive)
                .ToListAsync();

            var processComponents = await _db.WorkflowProcessComponents
                .Where(x => nodeIds.Contains(x.ProcessNodeId))
                .ToListAsync();

            return Ok(new
                {
                    SourceWorkflowId = workflow.Id,
                    workflow.TopPartId,
                    SourceWorkflowVersion = workflow.WorkflowVersion,
                    Status = workflow.Status,
                    Nodes = nodes,
                    Connections = connections,
                    Components = components,
                    ProcessComponents = processComponents
                });
        }

        [HttpPost("draft/save")]
            public async Task<IActionResult> SaveWorkflowDraft(
                SaveTopPartWorkflowDraftRequest dto)
            {
                var sourceWorkflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.SourceWorkflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (sourceWorkflow == null)
                    return NotFound("Avota TopPart Workflow nav atrasts.");

                if (sourceWorkflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");
                
                var duplicateComponentId = dto.Components
                    .GroupBy(x => x.Id)
                    .Any(x => x.Count() > 1);

                if (duplicateComponentId)
                    return BadRequest("Workflow komponentēm draftā jābūt unikāliem ID.");

                var duplicateComponent = dto.Components
                    .GroupBy(x => new
                    {
                        x.ComponentType,
                        x.TopPartId,
                        x.ItemId
                    })
                    .Any(x => x.Count() > 1);

                if (duplicateComponent)
                    return BadRequest("Viena un tā pati komponente Workflow drīkst būt pievienota tikai vienu reizi.");

                foreach (var component in dto.Components)
                    {
                        if (component.Quantity <= 0)
                            return BadRequest("Workflow komponentes daudzumam jābūt lielākam par 0.");

                        if (component.ComponentType != 1 &&
                                component.ComponentType != 2)
                            {
                                return BadRequest("Nezināms Workflow komponentes tips.");
                            }

                            if (component.ComponentType == 1 && component.ItemId != null)
                            {
                                return BadRequest("TopPart komponentei nedrīkst būt Item.");
                            }

                            if (component.ComponentType == 2 &&
                                (component.TopPartId != null || component.ReferencedWorkflowId != null))
                            {
                                return BadRequest("Item komponentei nedrīkst būt TopPart vai Workflow reference.");
                            }


                        if (component.ComponentType == 1)
                            {
                                if (component.TopPartId == sourceWorkflow.TopPartId)
                                    return BadRequest(
                                        "Workflow nevar izmantot pats savu TopPart kā komponenti.");
                                        
                                var referencedWorkflowExists = await _db.Workflows
                                    .AnyAsync(x =>
                                        x.Id == component.ReferencedWorkflowId &&
                                        x.TopPartId == component.TopPartId &&
                                        x.Status == WorkflowStatus.Released &&
                                        x.IsActive);

                                if (!referencedWorkflowExists)
                                    return BadRequest(
                                        "Norādītā Workflow versija nepieder izvēlētajam TopPart.");

                                var createsCycle = await CreatesWorkflowCycle(
                                    sourceWorkflow.TopPartId!.Value,
                                    component.ReferencedWorkflowId!.Value);

                                if (createsCycle)
                                    return BadRequest(
                                        "Workflow komponentu struktūra veido ciklisku atkarību.");
                            }


                        if (component.ComponentType == 1 &&
                            (component.TopPartId == null || component.ReferencedWorkflowId == null))
                        {
                            return BadRequest("TopPart komponentei jānorāda TopPart un tā Workflow versija.");
                        }

                        if (component.ComponentType == 2 &&
                            component.ItemId == null)
                        {
                            return BadRequest("Item komponentei jānorāda Item.");
                        }

                        if (component.ComponentType == 2)
                            {
                                var itemExists = await _db.Items
                                    .AnyAsync(x =>
                                        x.Id == component.ItemId &&
                                        x.IsActive);

                                if (!itemExists)
                                    return BadRequest("Norādītais Item nav atrasts vai nav aktīvs.");
                            }
                    }

                foreach (var node in dto.Nodes.Where(x => x.NodeType == 2))
                    {
                        foreach (var processComponent in node.ProcessComponents)
                        {
                            if (processComponent.Quantity <= 0)
                                return BadRequest("PROCESS komponentes daudzumam jābūt lielākam par 0.");

                            var workflowComponent = dto.Components
                                .FirstOrDefault(x => x.Id == processComponent.WorkflowComponentId);

                            if (workflowComponent == null)
                                return BadRequest("PROCESS satur komponenti, kas nav Workflow komponentu sarakstā.");
                        }
                    }

                foreach (var component in dto.Components)
                    {
                        var usedQuantity = dto.Nodes
                            .Where(x => x.NodeType == 2)
                            .SelectMany(x => x.ProcessComponents)
                            .Where(x => x.WorkflowComponentId == component.Id)
                            .Sum(x => x.Quantity);

                        if (usedQuantity > component.Quantity)
                            {
                                return BadRequest(
                                    "PROCESS izmantotais komponentes daudzums pārsniedz Workflow nepieciešamo daudzumu.");
                            }
                    }

                var sourceNodes = await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == sourceWorkflow.Id &&
                        x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();

                var sourceNodeIds = sourceNodes
                    .Select(x => x.Id)
                    .ToList();

                var sourceConnections = await _db.WorkflowNodeConnections
                    .Where(x =>
                        sourceNodeIds.Contains(x.FromNodeId) &&
                        sourceNodeIds.Contains(x.ToNodeId))
                    .ToListAsync();
                
                var sourceComponents = await _db.WorkflowComponents
                    .Where(x =>
                        x.WorkflowId == sourceWorkflow.Id &&
                        x.IsActive)
                    .ToListAsync();
                
                var sourceProcessComponents = await _db.WorkflowProcessComponents
                    .Where(x => sourceNodeIds.Contains(x.ProcessNodeId))
                    .ToListAsync();

               
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                await _db.WorkflowProcessComponents
                    .Where(x => sourceNodeIds.Contains(x.ProcessNodeId))
                    .ExecuteDeleteAsync();

                await _db.WorkflowNodeConnections
                    .Where(x =>
                        sourceNodeIds.Contains(x.FromNodeId) ||
                        sourceNodeIds.Contains(x.ToNodeId))
                    .ExecuteDeleteAsync();

                await _db.WorkflowComponents
                    .Where(x => x.WorkflowId == sourceWorkflow.Id)
                    .ExecuteDeleteAsync();

                await _db.WorkflowNodes
                    .Where(x => x.WorkflowId == sourceWorkflow.Id)
                    .ExecuteDeleteAsync();

                var nodeIdMap = new Dictionary<int, int>();
                var newNodes = new List<(int DraftId, WorkflowNode Node)>();

                foreach (var draftNode in dto.Nodes.OrderBy(x => x.SortOrder))
                {
                    var newNode = new WorkflowNode
                    {
                        WorkflowId = sourceWorkflow.Id,
                        NodeType = draftNode.NodeType,
                        Name = draftNode.Name,
                        TopPartId = draftNode.TopPartId,
                        WorkCenterId = draftNode.WorkCenterId,
                        EstimatedMinutes = draftNode.EstimatedMinutes,
                        Comments = draftNode.Comments,
                        SortOrder = draftNode.SortOrder,
                        IsActive = true
                    };

                    _db.WorkflowNodes.Add(newNode);
                    newNodes.Add((draftNode.Id, newNode));
                }

                await _db.SaveChangesAsync();

                foreach (var item in newNodes)
                    nodeIdMap[item.DraftId] = item.Node.Id;

                foreach (var draftConnection in dto.Connections)
                    {
                        if (!nodeIdMap.TryGetValue(draftConnection.FromNodeId, out var newFromNodeId) ||
                            !nodeIdMap.TryGetValue(draftConnection.ToNodeId, out var newToNodeId))
                        {
                            return BadRequest("Workflow Connection satur nezināmu mezgla ID.");
                        }

                        _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                        {
                            FromNodeId = newFromNodeId,
                            ToNodeId = newToNodeId
                        });
                    }

                await _db.SaveChangesAsync();

                var componentIdMap = new Dictionary<int, int>();
                var newComponents = new List<(int DraftId, WorkflowComponent Component)>();

                foreach (var draftComponent in dto.Components)
                {
                    var newComponent = new WorkflowComponent
                    {
                        WorkflowId = sourceWorkflow.Id,
                        ComponentType = draftComponent.ComponentType,
                        TopPartId = draftComponent.TopPartId,
                        ItemId = draftComponent.ItemId,
                        ReferencedWorkflowId = draftComponent.ReferencedWorkflowId,
                        Quantity = draftComponent.Quantity,
                        IsActive = true
                    };

                    _db.WorkflowComponents.Add(newComponent);
                    newComponents.Add((draftComponent.Id, newComponent));
                }

                await _db.SaveChangesAsync();

                foreach (var item in newComponents)
                    componentIdMap[item.DraftId] = item.Component.Id;

                foreach (var draftNode in dto.Nodes.Where(x => x.NodeType == 2))
                    {
                        if (!nodeIdMap.TryGetValue(draftNode.Id, out var newProcessNodeId))
                            return BadRequest("PROCESS mezglam nav atrasts jaunais Node ID.");

                        foreach (var processComponent in draftNode.ProcessComponents)
                        {
                            if (!componentIdMap.TryGetValue(
                                    processComponent.WorkflowComponentId,
                                    out var newWorkflowComponentId))
                            {
                                return BadRequest("PROCESS komponentei nav atrasts jaunais Component ID.");
                            }

                            _db.WorkflowProcessComponents.Add(new WorkflowProcessComponent
                            {
                                ProcessNodeId = newProcessNodeId,
                                WorkflowComponentId = newWorkflowComponentId,
                                Quantity = processComponent.Quantity
                            });
                        }
                    }

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
               
                return Ok(new
                    {
                        Saved = true,
                        WorkflowId = sourceWorkflow.Id
                    });
            }

            [HttpPost("{workflowId}/edit")]
            public async Task<IActionResult> EditWorkflow(int workflowId)
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();
                
                var sourceWorkflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (sourceWorkflow == null)
                    return NotFound("TopPart Workflow nav atrasts.");

                if (sourceWorkflow.Status != WorkflowStatus.Released)
                    return BadRequest("EDIT drīkst sākt tikai no RELEASED Workflow.");

                var draftExists = await _db.Workflows
                    .AnyAsync(x =>
                        x.TopPartId == sourceWorkflow.TopPartId &&
                        x.Status == WorkflowStatus.Draft &&
                        x.IsActive);

                if (draftExists)
                    return BadRequest("Šim TopPart jau eksistē DRAFT Workflow.");

                var nextVersion = await _db.Workflows
                    .Where(x => x.TopPartId == sourceWorkflow.TopPartId)
                    .MaxAsync(x => (int?)x.WorkflowVersion) ?? 0;

                nextVersion++;

                var draftWorkflow = new Workflow
                    {
                        TopPartId = sourceWorkflow.TopPartId,
                        WorkflowVersion = nextVersion,
                        Status = WorkflowStatus.Draft,
                        ParentWorkflowId = sourceWorkflow.Id,
                        VersionId = sourceWorkflow.VersionId,
                        ParentNodeId = sourceWorkflow.ParentNodeId,
                        Description = sourceWorkflow.Description,
                        Name = $"{sourceWorkflow.Name.Split(" - V")[0]} - V{nextVersion}",
                        CreatedDate = DateTime.Now,
                        IsCurrent = false,
                        IsActive = true
                    };

                _db.Workflows.Add(draftWorkflow);

                await _db.SaveChangesAsync();

                var sourceNodes = await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == sourceWorkflow.Id &&
                        x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();

                var sourceNodeIds = sourceNodes
                    .Select(x => x.Id)
                    .ToList();
                
                var nodeIdMap = new Dictionary<int, int>();

                foreach (var sourceNode in sourceNodes)
                {
                    var newNode = new WorkflowNode
                    {
                        WorkflowId = draftWorkflow.Id,
                        NodeType = sourceNode.NodeType,
                        Name = sourceNode.Name,
                        TopPartId = sourceNode.TopPartId,
                        WorkCenterId = sourceNode.WorkCenterId,
                        EstimatedMinutes = sourceNode.EstimatedMinutes,
                        Comments = sourceNode.Comments,
                        SortOrder = sourceNode.SortOrder,
                        IsActive = true
                    };

                    _db.WorkflowNodes.Add(newNode);

                    await _db.SaveChangesAsync();

                    nodeIdMap[sourceNode.Id] = newNode.Id;
                }

                var sourceConnections = await _db.WorkflowNodeConnections
                    .Where(x =>
                        sourceNodeIds.Contains(x.FromNodeId) &&
                        sourceNodeIds.Contains(x.ToNodeId))
                    .ToListAsync();

                foreach (var sourceConnection in sourceConnections)
                    {
                        _db.WorkflowNodeConnections.Add(new WorkflowNodeConnection
                        {
                            FromNodeId = nodeIdMap[sourceConnection.FromNodeId],
                            ToNodeId = nodeIdMap[sourceConnection.ToNodeId]
                        });
                    }

                await _db.SaveChangesAsync();

                var sourceComponents = await _db.WorkflowComponents
                    .Where(x =>
                        x.WorkflowId == sourceWorkflow.Id &&
                        x.IsActive)
                    .ToListAsync();

                var componentIdMap = new Dictionary<int, int>();

                foreach (var sourceComponent in sourceComponents)
                {
                    var newComponent = new WorkflowComponent
                    {
                        WorkflowId = draftWorkflow.Id,
                        ComponentType = sourceComponent.ComponentType,
                        TopPartId = sourceComponent.TopPartId,
                        ItemId = sourceComponent.ItemId,
                        ReferencedWorkflowId = sourceComponent.ReferencedWorkflowId,
                        Quantity = sourceComponent.Quantity,
                        IsActive = true
                    };

                    _db.WorkflowComponents.Add(newComponent);

                    await _db.SaveChangesAsync();

                    componentIdMap[sourceComponent.Id] = newComponent.Id;
                }

                var sourceProcessComponents = await _db.WorkflowProcessComponents
                    .Where(x => sourceNodeIds.Contains(x.ProcessNodeId))
                    .ToListAsync();

                foreach (var sourceProcessComponent in sourceProcessComponents)
                {
                    _db.WorkflowProcessComponents.Add(new WorkflowProcessComponent
                    {
                        ProcessNodeId = nodeIdMap[sourceProcessComponent.ProcessNodeId],
                        WorkflowComponentId = componentIdMap[sourceProcessComponent.WorkflowComponentId],
                        Quantity = sourceProcessComponent.Quantity
                    });
                }

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                    {
                        WorkflowId = draftWorkflow.Id,
                        draftWorkflow.WorkflowVersion,
                        draftWorkflow.Status,
                        draftWorkflow.IsCurrent,
                        draftWorkflow.ParentWorkflowId
                    });
            }

        [HttpPost("{workflowId}/release")]
            public async Task<IActionResult> ReleaseWorkflow(
                int workflowId,
                ReleaseTopPartWorkflowRequest dto)
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("TopPart Workflow nav atrasts.");
                
                if (string.IsNullOrWhiteSpace(dto.Description))
                    return BadRequest("Izmaiņu komentārs ir obligāts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("Release drīkst veikt tikai DRAFT Workflow.");

                if (await _workflowService.HasDependentDraftsAsync(
                        workflow.ParentWorkflowId ?? workflow.Id))
                    {
                        return BadRequest(
                            "Nevar RELEASE. Saistītajā TopPart ķēdē ir nepabeigts DRAFT. Vispirms pabeidziet šo DRAFT.");
                    }

                Workflow? parentWorkflow = null;

                if (workflow.ParentWorkflowId != null)
                {
                    parentWorkflow = await _db.Workflows
                        .FirstOrDefaultAsync(x =>
                            x.Id == workflow.ParentWorkflowId &&
                            x.Status == WorkflowStatus.Released &&
                            x.IsActive);

                    if (parentWorkflow == null)
                        return BadRequest("DRAFT avota RELEASED Workflow nav atrasts.");
                }

                List<WorkflowNode> parentNodes = new();

                if (parentWorkflow != null)
                    {
                        parentNodes = await _db.WorkflowNodes
                            .Where(x =>
                                x.WorkflowId == parentWorkflow.Id &&
                                x.IsActive)
                            .OrderBy(x => x.SortOrder)
                            .ToListAsync();
                    }
                    

                var nodes = await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == workflow.Id &&
                        x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ToListAsync();

                var lastNode = nodes.LastOrDefault();

                if (lastNode == null || lastNode.NodeType != (byte)WorkflowNodeType.Finish)
                    return BadRequest("Workflow jābeidzas ar FINISH mezglu.");
                
                var hasProcess = nodes.Any(
                    x => x.NodeType == (byte)WorkflowNodeType.Process);

                if (!hasProcess)
                    return BadRequest("Workflow jābūt vismaz vienam PROCESS mezglam.");

                var nodesChanged = parentWorkflow != null &&
                    (
                        parentNodes.Count != nodes.Count ||
                        parentNodes.Zip(nodes).Any(pair =>
                            pair.First.NodeType != pair.Second.NodeType ||
                            pair.First.Name != pair.Second.Name ||
                            pair.First.TopPartId != pair.Second.TopPartId ||
                            pair.First.WorkCenterId != pair.Second.WorkCenterId ||
                            pair.First.EstimatedMinutes != pair.Second.EstimatedMinutes ||
                            pair.First.Comments != pair.Second.Comments ||
                            pair.First.SortOrder != pair.Second.SortOrder)
                    );

                List<WorkflowComponent> parentComponents = new();

                if (parentWorkflow != null)
                    {
                        parentComponents = await _db.WorkflowComponents
                            .Where(x =>
                                x.WorkflowId == parentWorkflow.Id &&
                                x.IsActive)
                            .ToListAsync();
                    }
                

                var components = await _db.WorkflowComponents
                    .Where(x =>
                        x.WorkflowId == workflow.Id &&
                        x.IsActive)
                    .ToListAsync();
                
                foreach (var component in components.Where(x =>
                    x.ComponentType == 1 &&
                    x.ReferencedWorkflowId != null))
                {
                    if (component.TopPartId == workflow.TopPartId)
                        return BadRequest(
                            "Workflow nevar izmantot pats savu TopPart kā komponenti.");
                    
                    var referencedWorkflowValid = await _db.Workflows
                        .AnyAsync(x =>
                            x.Id == component.ReferencedWorkflowId!.Value &&
                            x.TopPartId == component.TopPartId &&
                            x.Status == WorkflowStatus.Released &&
                            x.IsActive);

                    if (!referencedWorkflowValid)
                        return BadRequest(
                            "TopPart komponente drīkst izmantot tikai RELEASED Workflow.");
                    
                    var createsCycle = await CreatesWorkflowCycle(
                        workflow.TopPartId!.Value,
                        component.ReferencedWorkflowId!.Value);

                    if (createsCycle)
                        return BadRequest(
                            "Workflow komponentu struktūra veido ciklisku atkarību.");
                }
                
                var componentsChanged = parentWorkflow != null &&
                    (
                        parentComponents.Count != components.Count ||
                        parentComponents.Any(parent =>
                            !components.Any(current =>
                                parent.ComponentType == current.ComponentType &&
                                parent.TopPartId == current.TopPartId &&
                                parent.ItemId == current.ItemId &&
                                parent.ReferencedWorkflowId == current.ReferencedWorkflowId &&
                                parent.Quantity == current.Quantity))
                    );

                var parentNodeIds = parentNodes
                    .Select(x => x.Id)
                    .ToList();
                
                var parentConnections = await _db.WorkflowNodeConnections
                    .Where(x =>
                        parentNodeIds.Contains(x.FromNodeId) &&
                        parentNodeIds.Contains(x.ToNodeId))
                    .ToListAsync();
                
                

                var parentProcessComponents = await _db.WorkflowProcessComponents
                    .Where(x => parentNodeIds.Contains(x.ProcessNodeId))
                    .ToListAsync();
               

                var nodeIds = nodes.Select(x => x.Id).ToList();

                var currentConnections = await _db.WorkflowNodeConnections
                    .Where(x =>
                        nodeIds.Contains(x.FromNodeId) &&
                        nodeIds.Contains(x.ToNodeId))
                    .ToListAsync();

                var finishNode = nodes
                    .FirstOrDefault(x =>
                        x.NodeType == (byte)WorkflowNodeType.Finish);

                if (finishNode == null)
                    return BadRequest("Workflow nav FINISH mezgla.");

                var incomingToFinish = currentConnections
                    .Where(x => x.ToNodeId == finishNode.Id)
                    .ToList();

                if (incomingToFinish.Count != 1)
                    {
                        return BadRequest(
                            "Pirms RELEASE paralēlie Workflow zari jāapvieno vienā plūsmā.");
                    }
                
                var processWithoutWip = nodes
                    .Where(x => x.NodeType == (byte)WorkflowNodeType.Process)
                    .Any(process =>
                        !currentConnections.Any(c =>
                            c.FromNodeId == process.Id &&
                            nodes.Any(n =>
                                n.Id == c.ToNodeId &&
                                n.NodeType == (byte)WorkflowNodeType.Wip)));

                if (processWithoutWip)
                    return BadRequest("Katram PROCESS mezglam jābeidzas ar WIP mezglu.");
                
                var wipWithoutProcess = nodes
                    .Where(x => x.NodeType == (byte)WorkflowNodeType.Wip)
                    .Any(wip =>
                        !currentConnections.Any(c =>
                            c.ToNodeId == wip.Id &&
                            nodes.Any(n =>
                                n.Id == c.FromNodeId &&
                                n.NodeType == (byte)WorkflowNodeType.Process)));

                if (wipWithoutProcess)
                    return BadRequest("Katram WIP mezglam jābūt PROCESS rezultātam.");

                var wipWithoutNextNode = nodes
                    .Where(x => x.NodeType == (byte)WorkflowNodeType.Wip)
                    .Any(wip =>
                        !currentConnections.Any(c =>
                            c.FromNodeId == wip.Id));

                if (wipWithoutNextNode)
                    return BadRequest("Katram WIP mezglam jābūt nākamajam mezglam.");

                var wipWithInvalidNextNode = nodes
                    .Where(x => x.NodeType == (byte)WorkflowNodeType.Wip)
                    .Any(wip =>
                        currentConnections
                            .Where(c => c.FromNodeId == wip.Id)
                            .Any(c =>
                                !nodes.Any(n =>
                                    n.Id == c.ToNodeId &&
                                    (n.NodeType == (byte)WorkflowNodeType.Process ||
                                    n.NodeType == (byte)WorkflowNodeType.Finish))));

                if (wipWithInvalidNextNode)
                    return BadRequest("WIP mezgls drīkst turpināties tikai uz PROCESS vai FINISH mezglu.");

                var connectionsChanged = parentWorkflow != null &&
                    (
                        parentConnections.Count != currentConnections.Count ||
                        parentConnections.Any(parentConnection =>
                        {
                            var parentFromNode = parentNodes
                                .First(x => x.Id == parentConnection.FromNodeId);

                            var parentToNode = parentNodes
                                .First(x => x.Id == parentConnection.ToNodeId);

                            return !currentConnections.Any(currentConnection =>
                            {
                                var currentFromNode = nodes
                                    .First(x => x.Id == currentConnection.FromNodeId);

                                var currentToNode = nodes
                                    .First(x => x.Id == currentConnection.ToNodeId);

                                return
                                    parentFromNode.SortOrder == currentFromNode.SortOrder &&
                                    parentToNode.SortOrder == currentToNode.SortOrder;
                            });
                        })
                    );

                var processComponents = await _db.WorkflowProcessComponents
                    .Where(x => nodeIds.Contains(x.ProcessNodeId))
                    .ToListAsync();

                foreach (var component in components)
                {
                    var usedQuantity = processComponents
                        .Where(x => x.WorkflowComponentId == component.Id)
                        .Sum(x => x.Quantity);

                    if (usedQuantity != component.Quantity)
                        return BadRequest(
                            "Visam Workflow komponentes daudzumam jābūt piesaistītam PROCESS mezgliem.");
                }

            

            var processComponentsChanged = parentWorkflow != null &&
                (
                    parentProcessComponents.Count != processComponents.Count ||
                    parentProcessComponents.Any(parentLink =>
                {
                    var parentNode = parentNodes
                        .First(x => x.Id == parentLink.ProcessNodeId);

                    var parentComponent = parentComponents
                        .First(x => x.Id == parentLink.WorkflowComponentId);                  

                    return !processComponents.Any(currentLink =>
                    {
                        var currentNode = nodes
                            .First(x => x.Id == currentLink.ProcessNodeId);

                        var currentComponent = components
                            .First(x => x.Id == currentLink.WorkflowComponentId);

                        return
                            parentNode.SortOrder == currentNode.SortOrder &&
                            parentComponent.ComponentType == currentComponent.ComponentType &&
                            parentComponent.TopPartId == currentComponent.TopPartId &&
                            parentComponent.ItemId == currentComponent.ItemId &&
                            parentComponent.ReferencedWorkflowId == currentComponent.ReferencedWorkflowId &&
                            parentLink.Quantity == currentLink.Quantity;
                    });
                 })
            );

            if (parentWorkflow != null &&
                !nodesChanged &&
                !componentsChanged &&
                !connectionsChanged &&
                !processComponentsChanged)

            {
                return BadRequest(
                    "Workflow nav tehnoloģisku izmaiņu. Jauna RELEASED versija netiks izveidota.");
            }
                
                await _db.Workflows
                    .Where(x =>
                        x.TopPartId == workflow.TopPartId &&
                        x.Status == WorkflowStatus.Released &&
                        x.IsCurrent)
                    .ExecuteUpdateAsync(x =>
                        x.SetProperty(w => w.IsCurrent, false));

                workflow.Description = dto.Description.Trim();
                workflow.Status = WorkflowStatus.Released;
                workflow.ReleasedDate = DateTime.Now;
                workflow.IsCurrent = true;

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                await _workflowService.PropagateReleasedWorkflowAsync(workflow.Id);

                return Ok(new
                    {
                        WorkflowId = workflow.Id,
                        workflow.WorkflowVersion,
                        workflow.Status,
                        workflow.IsCurrent
                    });
            }

            [HttpGet("{workflowId}/history")]
                public async Task<IActionResult> GetWorkflowHistory(int workflowId)
                {
                    var workflow = await _db.Workflows
                        .FirstOrDefaultAsync(x =>
                            x.Id == workflowId &&
                            x.IsActive);

                    if (workflow == null)
                        return NotFound("Workflow nav atrasts.");

                    var history = await _db.Workflows
                        .Where(x =>
                            x.TopPartId == workflow.TopPartId &&
                            x.IsActive)
                        .OrderBy(x => x.WorkflowVersion)
                        .Select(x => new
                        {
                            x.Id,
                            x.WorkflowVersion,
                            x.Status,
                            x.ParentWorkflowId,
                            x.Name,
                            x.IsCurrent
                        })
                        .ToListAsync();

                    return Ok(history);
                }

        [HttpDelete("{workflowId}/draft")]
            public async Task<IActionResult> DeleteWorkflowDraft(int workflowId)
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("TopPart Workflow nav atrasts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("Dzēst drīkst tikai DRAFT Workflow.");
                
                if (workflow.ParentWorkflowId == null)
                    return BadRequest("Sākotnējo Workflow DRAFT dzēst nedrīkst.");

                var nodes = await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == workflow.Id &&
                        x.IsActive)
                    .ToListAsync();

                var nodeIds = nodes
                    .Select(x => x.Id)
                    .ToList();

                var processComponents = await _db.WorkflowProcessComponents
                    .Where(x => nodeIds.Contains(x.ProcessNodeId))
                    .ToListAsync();

                _db.WorkflowProcessComponents.RemoveRange(processComponents);

                await _db.SaveChangesAsync();

                var connections = await _db.WorkflowNodeConnections
                    .Where(x =>
                        nodeIds.Contains(x.FromNodeId) ||
                        nodeIds.Contains(x.ToNodeId))
                    .ToListAsync();

                _db.WorkflowNodeConnections.RemoveRange(connections);

                await _db.SaveChangesAsync();

                var components = await _db.WorkflowComponents
                    .Where(x => x.WorkflowId == workflow.Id)
                    .ToListAsync();

                _db.WorkflowComponents.RemoveRange(components);

                await _db.SaveChangesAsync();

                _db.WorkflowNodes.RemoveRange(nodes);

                await _db.SaveChangesAsync();

                _db.Workflows.Remove(workflow);

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                return NoContent();
            }

       private async Task<bool> CreatesWorkflowCycle(
                uint sourceTopPartId,
                int referencedWorkflowId)
            {
                var visited = new HashSet<int>();
                var stack = new Stack<int>();

                stack.Push(referencedWorkflowId);

                while (stack.Count > 0)
                {
                    var workflowId = stack.Pop();

                    if (!visited.Add(workflowId))
                        continue;

                    var workflowTopPartId = await _db.Workflows
                        .Where(x =>
                            x.Id == workflowId &&
                            x.IsActive)
                        .Select(x => x.TopPartId)
                        .FirstOrDefaultAsync();
                    
                    if (workflowTopPartId == sourceTopPartId)
                        return true;

                    var referencedWorkflowIds = await _db.WorkflowComponents
                        .Where(x =>
                            x.WorkflowId == workflowId &&
                            x.ComponentType == 1 &&
                            x.ReferencedWorkflowId != null &&
                            x.IsActive)
                        .Select(x => x.ReferencedWorkflowId!.Value)
                        .ToListAsync();

                    foreach (var id in referencedWorkflowIds)
                        stack.Push(id);
                }

                return false;
            }

        [HttpGet("toppart/{topPartId}/display")]
            public async Task<IActionResult> GetDisplayWorkflow(int topPartId)
            {
                var workflow = await _db.Workflows
                    .Where(x =>
                        x.TopPartId == topPartId &&
                        x.IsActive)
                    .OrderBy(x => x.Status == WorkflowStatus.Draft ? 0 : 1)
                    .ThenByDescending(x => x.IsCurrent)
                    .ThenByDescending(x => x.WorkflowVersion)
                    .FirstOrDefaultAsync();

                if (workflow == null)
                    return NotFound("TopPart Workflow nav atrasts.");

                return Ok(workflow.Id);
            }
        
        [HttpGet("{workflowId}/bom")]
            public async Task<IActionResult> GetBom(int workflowId)
            {
                var workflowExists = await _db.Workflows
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == workflowId &&
                        x.IsActive);

                if (!workflowExists)
                    return NotFound("Workflow nav atrasts.");

                var rows = await (
                    from component in _db.WorkflowComponents.AsNoTracking()

                    
                    join item in _db.Items.AsNoTracking()
                            on component.ItemId equals (int?)item.Id into items

                        from item in items.DefaultIfEmpty()



                    where component.WorkflowId == workflowId
                        && component.IsActive

                    orderby component.Id

                    select new TopPartWorkflowBomDto
                        {
                            Id = component.Id,
                            ComponentType = component.ComponentType,
                            TopPartId = component.TopPartId,
                            ItemId = component.ItemId,
                            ReferencedWorkflowId = component.ReferencedWorkflowId,
                            Quantity = component.Quantity,

                            UsedQuantity = _db.WorkflowProcessComponents
                                .Where(x => x.WorkflowComponentId == component.Id)
                                .Sum(x => (decimal?)x.Quantity) ?? 0,

                            RemainingQuantity =
                                component.Quantity -
                                (_db.WorkflowProcessComponents
                                    .Where(x => x.WorkflowComponentId == component.Id)
                                    .Sum(x => (decimal?)x.Quantity) ?? 0),



                            ItemCode = item != null ? item.ItemCode : null,
                            ItemName = item != null ? item.ItemName : null,
                            ItemUnit = item != null ? item.Unit : null
                        }
                ).ToListAsync();

                var topPartIds = rows
                    .Where(x => x.ComponentType == 1 && x.TopPartId.HasValue)
                    .Select(x => (int)x.TopPartId!.Value)
                    .Distinct()
                    .ToList();

                var topParts = await _db.TopParts
                    .AsNoTracking()
                    .Where(x => topPartIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                foreach (var row in rows)
                {
                    if (row.ComponentType != 1 || !row.TopPartId.HasValue)
                        continue;

                    if (topParts.TryGetValue((int)row.TopPartId.Value, out var topPart))
                    {
                        row.TopPartCode = topPart.TopPartCode;
                        row.TopPartName = topPart.TopPartName;
                    }
                }

                return Ok(rows);
            }

        [HttpGet("{workflowId}/bom/parts")]
        public async Task<IActionResult> GetBomPartOptions(int workflowId)
            {
                var workflow = await _db.Workflows
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("TopPart Workflow nav atrasts.");

                var rows = await _db.TopParts
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.TopPartType == TopPartType.Part &&
                        x.Id != workflow.TopPartId &&
                        _db.Workflows.Any(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Released &&
                            w.IsActive))
                    .Select(x => new TopPartBomPartOptionDto
                    {
                        TopPartId = x.Id,
                        TopPartCode = x.TopPartCode,
                        TopPartName = x.TopPartName,

                        ReleasedWorkflowId = _db.Workflows
                            .Where(w =>
                                w.TopPartId == x.Id &&
                                w.Status == WorkflowStatus.Released &&
                                w.IsActive)
                            .OrderByDescending(w => w.WorkflowVersion)
                            .Select(w => (int?)w.Id)
                            .FirstOrDefault(),

                        ReleasedWorkflowVersion = _db.Workflows
                            .Where(w =>
                                w.TopPartId == x.Id &&
                                w.Status == WorkflowStatus.Released &&
                                w.IsActive &&
                                w.IsCurrent)
                            .Select(w => (int?)w.WorkflowVersion)
                            .FirstOrDefault(),

                        HasDraft = _db.Workflows.Any(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Draft &&
                            w.IsActive)
                    })
                    .ToListAsync();

                foreach (var row in rows)
                    row.CanAdd = row.ReleasedWorkflowId.HasValue;

                return Ok(rows);
            }

        [HttpPost("{workflowId}/bom/part")]
        public async Task<IActionResult> AddBomPart(
            int workflowId,
            AddTopPartBomPartRequest dto)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(x =>
                    x.Id == workflowId &&
                    x.IsActive);

            if (workflow == null)
                return NotFound("Workflow nav atrasts.");

            if (workflow.Status != WorkflowStatus.Draft)
                return BadRequest("RELEASED Workflow modificēt nedrīkst.");

            if (dto.Quantity <= 0)
                return BadRequest("Daudzumam jābūt lielākam par 0.");

            if (workflow.TopPartId == dto.TopPartId)
                return BadRequest("Workflow nevar izmantot pats savu TopPart kā komponenti.");

            var releasedWorkflow = await _db.Workflows
                .Where(x =>
                    x.TopPartId == dto.TopPartId &&
                    x.Status == WorkflowStatus.Released &&
                    x.IsCurrent &&
                    x.IsActive)
                .FirstOrDefaultAsync();

            if (releasedWorkflow == null)
                return BadRequest("Izvēlētajam TopPart nav aktuāla RELEASED Workflow.");
            
            var createsCycle = await CreatesWorkflowCycle(
                workflow.TopPartId!.Value,
                releasedWorkflow.Id);

            if (createsCycle)
                return BadRequest(
                    "Workflow komponentu struktūra veido ciklisku atkarību.");

            var exists = await _db.WorkflowComponents.AnyAsync(x =>
                x.WorkflowId == workflowId &&
                x.ComponentType == 1 &&
                x.TopPartId == dto.TopPartId &&
                x.IsActive);

            if (exists)
                return BadRequest("Šis TopPart jau ir pievienots Workflow.");

            var component = new WorkflowComponent
            {
                WorkflowId = workflowId,
                ComponentType = 1,
                TopPartId = (uint)dto.TopPartId,
                ItemId = null,
                ReferencedWorkflowId = releasedWorkflow.Id,
                Quantity = dto.Quantity,
                IsActive = true
            };

            _db.WorkflowComponents.Add(component);
            await _db.SaveChangesAsync();

            return Ok(component);
        }

        [HttpPost("{workflowId}/bom/item")]
        public async Task<IActionResult> AddBomItem(
            int workflowId,
            AddTopPartBomItemRequest dto)
        {
            var workflow = await _db.Workflows
                .FirstOrDefaultAsync(x =>
                    x.Id == workflowId &&
                    x.IsActive);

            if (workflow == null)
                return NotFound("Workflow nav atrasts.");

            if (workflow.Status != WorkflowStatus.Draft)
                return BadRequest("RELEASED Workflow modificēt nedrīkst.");

            if (dto.Quantity <= 0)
                return BadRequest("Daudzumam jābūt lielākam par 0.");

            var itemExists = await _db.Items
                .AnyAsync(x =>
                    x.Id == dto.ItemId &&
                    x.IsActive);

            if (!itemExists)
                return BadRequest("Norādītais Item nav atrasts vai nav aktīvs.");

            var exists = await _db.WorkflowComponents.AnyAsync(x =>
                x.WorkflowId == workflowId &&
                x.ComponentType == 2 &&
                x.ItemId == dto.ItemId &&
                x.IsActive);

            if (exists)
                return BadRequest("Šis Item jau ir pievienots Workflow.");

            var component = new WorkflowComponent
            {
                WorkflowId = workflowId,
                ComponentType = 2,
                TopPartId = null,
                ItemId = dto.ItemId,
                ReferencedWorkflowId = null,
                Quantity = dto.Quantity,
                IsActive = true
            };

            _db.WorkflowComponents.Add(component);
            await _db.SaveChangesAsync();

            return Ok(component);
        }

        [HttpGet("{workflowId}/bom/parts/selector")]
            public async Task<IActionResult> GetBomPartSelector(int workflowId)
            {
                var workflow = await _db.Workflows
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("TopPart Workflow nav atrasts.");

                var existingParts = await _db.WorkflowComponents
                    .AsNoTracking()
                    .Where(x =>
                        x.WorkflowId == workflowId &&
                        x.ComponentType == 1 &&
                        x.TopPartId != null &&
                        x.IsActive)
                    .ToListAsync();

                var availableParts = await _db.TopParts
                    .AsNoTracking()
                    .Where(x =>
                        x.IsActive &&
                        x.TopPartType == TopPartType.Part &&
                        x.Id != workflow.TopPartId &&
                        _db.Workflows.Any(w =>
                            w.TopPartId == x.Id &&
                            w.Status == WorkflowStatus.Released &&
                            w.IsCurrent &&
                            w.IsActive))
                    .OrderBy(x => x.TopPartCode)
                    .Select(x => new
                    {
                        x.Id,
                        x.TopPartCode,
                        x.TopPartName,

                        ReleasedWorkflowId = _db.Workflows
                            .Where(w =>
                                w.TopPartId == x.Id &&
                                w.Status == WorkflowStatus.Released &&
                                w.IsCurrent &&
                                w.IsActive)
                            .Select(w => w.Id)
                            .First(),

                        ReleasedWorkflowVersion = _db.Workflows
                            .Where(w =>
                                w.TopPartId == x.Id &&
                                w.Status == WorkflowStatus.Released &&
                                w.IsCurrent &&
                                w.IsActive)
                            .Select(w => w.WorkflowVersion)
                            .First()
                    })
                    .ToListAsync();

                var rows = availableParts
                    .Select(x =>
                    {
                        var existing = existingParts
                            .FirstOrDefault(c => c.TopPartId == (uint)x.Id);

                        return new TopPartBomPartSelectorDto
                        {
                            TopPartId = x.Id,
                            TopPartCode = x.TopPartCode,
                            TopPartName = x.TopPartName,
                            ReleasedWorkflowId = x.ReleasedWorkflowId,
                            ReleasedWorkflowVersion = x.ReleasedWorkflowVersion,
                            IsSelected = existing != null,
                            Quantity = existing?.Quantity ?? 1,
                            CanEdit = workflow.Status == WorkflowStatus.Draft
                        };
                    })
                    .ToList();

                return Ok(rows);
            }

        [HttpPut("{workflowId}/bom/parts")]
            public async Task<IActionResult> SaveBomParts(
                int workflowId,
                SaveTopPartBomPartsRequest dto)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.TopPartId != null &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("Workflow nav atrasts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");

                if (dto.Parts.Any(x => x.Quantity <= 0))
                    return BadRequest("Daudzumam jābūt lielākam par 0.");

                if (dto.Parts
                    .GroupBy(x => x.TopPartId)
                    .Any(x => x.Count() > 1))
                {
                    return BadRequest("Viens TopPart BOM sarakstā drīkst būt tikai vienu reizi.");
                }

                var existingParts = await _db.WorkflowComponents
                    .Where(x =>
                        x.WorkflowId == workflowId &&
                        x.ComponentType == 1 &&
                        x.IsActive)
                    .ToListAsync();

                var bomChanged =
                    existingParts.Count != dto.Parts.Count ||
                    existingParts.Any(existing =>
                    {
                        var part = dto.Parts.FirstOrDefault(x =>
                            existing.TopPartId == (uint)x.TopPartId);

                        return part == null ||
                            existing.Quantity != part.Quantity;
                    });

                // Noņemam PART, kurus lietotājs izķeksējis.
                var selectedIds = dto.Parts
                    .Select(x => x.TopPartId)
                    .ToHashSet();

                var removedParts = existingParts
                    .Where(x =>
                        x.TopPartId.HasValue &&
                        !selectedIds.Contains((int)x.TopPartId.Value))
                    .ToList();
                
                if (bomChanged)
                    {
                        var componentIds = existingParts
                            .Select(x => x.Id)
                            .ToList();

                        await _db.WorkflowProcessComponents
                            .Where(x => componentIds.Contains(x.WorkflowComponentId))
                            .ExecuteDeleteAsync();
                    }

                _db.WorkflowComponents.RemoveRange(removedParts);

                // Pievienojam jaunos un mainām esošo daudzumu.
                foreach (var part in dto.Parts)
                {
                    var existing = existingParts.FirstOrDefault(x =>
                        x.TopPartId == (uint)part.TopPartId);

                    if (existing != null)
                    {
                        existing.Quantity = part.Quantity;
                        continue;
                    }

                    if (workflow.TopPartId == (uint)part.TopPartId)
                        return BadRequest(
                            "Workflow nevar izmantot pats savu TopPart kā komponenti.");

                    var releasedWorkflow = await _db.Workflows
                        .FirstOrDefaultAsync(x =>
                            x.TopPartId == part.TopPartId &&
                            x.Status == WorkflowStatus.Released &&
                            x.IsCurrent &&
                            x.IsActive);

                    if (releasedWorkflow == null)
                        return BadRequest(
                            "Izvēlētajam TopPart nav aktuāla RELEASED Workflow.");

                    var createsCycle = await CreatesWorkflowCycle(
                        workflow.TopPartId!.Value,
                        releasedWorkflow.Id);

                    if (createsCycle)
                        return BadRequest(
                            "Workflow komponentu struktūra veido ciklisku atkarību.");

                    _db.WorkflowComponents.Add(new WorkflowComponent
                    {
                        WorkflowId = workflowId,
                        ComponentType = 1,
                        TopPartId = (uint)part.TopPartId,
                        ItemId = null,
                        ReferencedWorkflowId = releasedWorkflow.Id,
                        Quantity = part.Quantity,
                        IsActive = true
                    });
                }

                await _db.SaveChangesAsync();

                return NoContent();
            }

            [HttpGet("{workflowId}/bom/items/selector")]
                public async Task<IActionResult> GetBomItemSelector(int workflowId)
                {
                    var workflow = await _db.Workflows
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x =>
                            x.Id == workflowId &&
                            x.IsActive);

                    if (workflow == null)
                        return NotFound("Workflow nav atrasts.");

                    var existingItems = await _db.WorkflowComponents
                        .AsNoTracking()
                        .Where(x =>
                            x.WorkflowId == workflowId &&
                            x.ComponentType == 2 &&
                            x.ItemId != null &&
                            x.IsActive)
                        .ToListAsync();

                    var availableItems = await _db.Items
                        .AsNoTracking()
                        .Where(x => x.IsActive)
                        .OrderBy(x => x.ItemCode)
                        .Select(x => new
                        {
                            x.Id,
                            x.ItemCode,
                            x.ItemName,
                            x.Unit
                        })
                        .ToListAsync();

                    var rows = availableItems
                        .Select(x =>
                        {
                            var existing = existingItems
                                .FirstOrDefault(c => c.ItemId == x.Id);

                            return new TopPartBomItemSelectorDto
                            {
                                ItemId = x.Id,
                                ItemCode = x.ItemCode,
                                ItemName = x.ItemName,
                                Unit = x.Unit,
                                IsSelected = existing != null,
                                Quantity = existing?.Quantity ?? 1,
                                CanEdit = workflow.Status == WorkflowStatus.Draft
                            };
                        })
                        .ToList();

                    return Ok(rows);
                }

            [HttpPut("{workflowId}/bom/items")]
            public async Task<IActionResult> SaveBomItems(
                int workflowId,
                SaveTopPartBomItemsRequest dto)
            {
                var workflow = await _db.Workflows
                    .FirstOrDefaultAsync(x =>
                        x.Id == workflowId &&
                        x.IsActive);

                if (workflow == null)
                    return NotFound("Workflow nav atrasts.");

                if (workflow.Status != WorkflowStatus.Draft)
                    return BadRequest("RELEASED Workflow modificēt nedrīkst.");

                if (dto.Items.Any(x => x.Quantity <= 0))
                    return BadRequest("Daudzumam jābūt lielākam par 0.");

                if (dto.Items
                    .GroupBy(x => x.ItemId)
                    .Any(x => x.Count() > 1))
                {
                    return BadRequest(
                        "Viens ITEM BOM sarakstā drīkst būt tikai vienu reizi.");
                }

                var existingItems = await _db.WorkflowComponents
                    .Where(x =>
                        x.WorkflowId == workflowId &&
                        x.ComponentType == 2 &&
                        x.IsActive)
                    .ToListAsync();
                
                var bomChanged =
                    existingItems.Count != dto.Items.Count ||
                    existingItems.Any(existing =>
                    {
                        var item = dto.Items.FirstOrDefault(x =>
                            existing.ItemId == x.ItemId);

                        return item == null ||
                            existing.Quantity != item.Quantity;
                    });

                var selectedIds = dto.Items
                    .Select(x => x.ItemId)
                    .ToHashSet();

                var removedItems = existingItems
                    .Where(x =>
                        x.ItemId.HasValue &&
                        !selectedIds.Contains(x.ItemId.Value))
                    .ToList();

                if (bomChanged)
                    {
                        var componentIds = existingItems
                            .Select(x => x.Id)
                            .ToList();

                        await _db.WorkflowProcessComponents
                            .Where(x => componentIds.Contains(x.WorkflowComponentId))
                            .ExecuteDeleteAsync();
                    }

                _db.WorkflowComponents.RemoveRange(removedItems);

                foreach (var item in dto.Items)
                {
                    var existing = existingItems
                        .FirstOrDefault(x => x.ItemId == item.ItemId);

                    if (existing != null)
                    {
                        existing.Quantity = item.Quantity;
                        continue;
                    }

                    var itemExists = await _db.Items.AnyAsync(x =>
                        x.Id == item.ItemId &&
                        x.IsActive);

                    if (!itemExists)
                        return BadRequest("Izvēlētais ITEM nav atrasts.");

                    _db.WorkflowComponents.Add(new WorkflowComponent
                    {
                        WorkflowId = workflowId,
                        ComponentType = 2,
                        TopPartId = null,
                        ItemId = item.ItemId,
                        ReferencedWorkflowId = null,
                        Quantity = item.Quantity,
                        IsActive = true
                    });
                }

                await _db.SaveChangesAsync();

                return NoContent();
            }

        [HttpGet("workcenters")]
        public async Task<IActionResult> GetWorkCenters()
        {
            var workCenters = await _db.WorkCenters
                .Where(x => x.IsActive)
                .OrderBy(x => x.WorkCenter_Order)
                .Select(x => new WorkCenterOptionDto
                {
                    Id = x.Id,
                    Name = x.WorkCentr_Name,
                    Code = x.WorkCentr_Code
                })
                .ToListAsync();

            return Ok(workCenters);
        }

        [HttpGet("steptypes")]
        public async Task<IActionResult> GetStepTypes()
        {
            var stepTypes = await _db.StepTypes
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .Select(x => new StepTypeOptionDto
                {
                    Id = (uint)x.Id,
                    Name = x.StepTypeName
                })
                .ToListAsync();

            return Ok(stepTypes);
        }

    }
}