using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;
using ManiApi.DTOs.WorkFlow;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TopPartWorkflowController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TopPartWorkflowController(AppDbContext db)
        {
            _db = db;
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
                        IsCurrent = false,
                        IsActive = true
                    };

                _db.Workflows.Add(workflow);

                await _db.SaveChangesAsync();

                var partNode = new WorkflowNode
                    {
                        WorkflowId = workflow.Id,
                        NodeType = 1,
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
                        NodeType = 4,
                        Name = "FINISH",
                        TopPartId = dto.TopPartId,
                        SortOrder = 20,
                        IsActive = true
                    };

                _db.WorkflowNodes.Add(finishNode);

                await _db.SaveChangesAsync();

                var connection = new WorkflowNodeConnection
                    {
                        FromNodeId = partNode.Id,
                        ToNodeId = finishNode.Id
                    };

                _db.WorkflowNodeConnections.Add(connection);

                await _db.SaveChangesAsync();

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
                    Workflow = new
                    {
                        workflow.Id,
                        workflow.TopPartId,
                        workflow.WorkflowVersion,
                        workflow.Status,
                        workflow.IsCurrent,
                        workflow.Name
                    },
                    Nodes = nodes,
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
                
                var selectedNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.SelectedNodeId &&
                        x.WorkflowId == workflow.Id &&
                        x.IsActive);

                if (selectedNode == null)
                    return BadRequest("Izvēlētais mezgls nav atrasts šajā Workflow.");
                
                if (selectedNode.NodeType != 1 &&
                        selectedNode.NodeType != 2)
                    {
                        return BadRequest("PROCESS var pievienot tikai aiz PART vai PROCESS mezgla.");
                    }
                
                var oldConnection = await _db.WorkflowNodeConnections
                    .FirstOrDefaultAsync(x =>
                        x.FromNodeId == selectedNode.Id);

                if (oldConnection == null)
                    return BadRequest("Izvēlētajam mezglam nav nākamā mezgla.");
                
                var nextNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == oldConnection.ToNodeId &&
                        x.WorkflowId == workflow.Id &&
                        x.IsActive);

                if (nextNode == null)
                    return BadRequest("Nākamais mezgls nav atrasts.");

                await _db.WorkflowNodes
                    .Where(x =>
                        x.WorkflowId == workflow.Id &&
                        x.SortOrder > selectedNode.SortOrder &&
                        x.IsActive)
                    .ExecuteUpdateAsync(x =>
                        x.SetProperty(n => n.SortOrder, n => n.SortOrder + 10));

                var processNode = new WorkflowNode
                    {
                        WorkflowId = workflow.Id,
                        NodeType = 2,
                        Name = dto.ProcessName,
                        TopPartId = workflow.TopPartId,
                        WorkCenterId = dto.WorkCenterId,
                        EstimatedMinutes = dto.EstimatedMinutes,
                        SortOrder = selectedNode.SortOrder + 10,
                        IsActive = true
                    };

                _db.WorkflowNodes.Add(processNode);

                await _db.SaveChangesAsync();

                _db.WorkflowNodeConnections.Remove(oldConnection);

                _db.WorkflowNodeConnections.AddRange(
                    new WorkflowNodeConnection
                    {
                        FromNodeId = selectedNode.Id,
                        ToNodeId = processNode.Id
                    },
                    new WorkflowNodeConnection
                    {
                        FromNodeId = processNode.Id,
                        ToNodeId = nextNode.Id
                    });

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(processNode);
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
                
                var processNode = await _db.WorkflowNodes
                    .FirstOrDefaultAsync(x =>
                        x.Id == dto.ProcessNodeId &&
                        x.WorkflowId == dto.WorkflowId &&
                        x.NodeType == 2 &&
                        x.IsActive);

                if (processNode == null)
                    return NotFound("PROCESS mezgls nav atrasts.");

                processNode.Name = dto.ProcessName;
                processNode.WorkCenterId = dto.WorkCenterId;
                processNode.EstimatedMinutes = dto.EstimatedMinutes;

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
                            x.NodeType == 2 &&
                            x.IsActive);

                    if (processNode == null)
                        return NotFound("PROCESS mezgls nav atrasts.");
                    
                    var incomingConnection = await _db.WorkflowNodeConnections
                        .FirstOrDefaultAsync(x =>
                            x.ToNodeId == processNode.Id);

                    if (incomingConnection == null)
                        return BadRequest("PROCESS mezglam nav iepriekšējā savienojuma.");
                    
                    var outgoingConnection = await _db.WorkflowNodeConnections
                        .FirstOrDefaultAsync(x =>
                            x.FromNodeId == processNode.Id);

                    if (outgoingConnection == null)
                        return BadRequest("PROCESS mezglam nav nākamā savienojuma.");

                    var newConnection = new WorkflowNodeConnection
                        {
                            FromNodeId = incomingConnection.FromNodeId,
                            ToNodeId = outgoingConnection.ToNodeId
                        };

                    _db.WorkflowNodeConnections.Add(newConnection);

                    _db.WorkflowNodeConnections.Remove(incomingConnection);
                    _db.WorkflowNodeConnections.Remove(outgoingConnection);

                    await _db.SaveChangesAsync();

                    _db.WorkflowNodes.Remove(processNode);

                    await _db.SaveChangesAsync();

                    await _db.WorkflowNodes
                        .Where(x =>
                            x.WorkflowId == workflowId &&
                            x.SortOrder > processNode.SortOrder &&
                            x.IsActive)
                        .ExecuteUpdateAsync(x =>
                            x.SetProperty(n => n.SortOrder, n => n.SortOrder - 10));

                    await transaction.CommitAsync();

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
                        Name = $"{sourceWorkflow.Name.Split(" - V")[0]} - V{nextVersion}",
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
            public async Task<IActionResult> ReleaseWorkflow(int workflowId)
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
                    return BadRequest("Release drīkst veikt tikai DRAFT Workflow.");

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

                if (lastNode == null || lastNode.NodeType != 4)
                    return BadRequest("Workflow jābeidzas ar FINISH mezglu.");

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

                workflow.Status = WorkflowStatus.Released;

                workflow.IsCurrent = true;

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

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

    }
}