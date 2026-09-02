using ManaApp.Shared.DTOs.Tasks;
using ManiApi.Data;
using Microsoft.AspNetCore.Mvc;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;
using ManiApi.Services.Planning;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/tasks-new")]
    public class TasksNewController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ProductionRequirementService _productionRequirementService;

        public TasksNewController(
            AppDbContext db,
            ProductionRequirementService productionRequirementService)
        {
            _db = db;
            _productionRequirementService = productionRequirementService;
        }

        
    [HttpPut("{taskId:int}/employee")]
    public async Task<IActionResult> AssignEmployee(
        int taskId,
        [FromBody] AssignTaskNewEmployeeRequest dto)
    {
        if (taskId <= 0)
            return BadRequest("Task ID nav derīgs.");

        var task = await _db.TasksNew
            .FirstOrDefaultAsync(x =>
                x.ID == (uint)taskId &&
                x.IsActive);

        if (task == null)
            return NotFound("Task nav atrasts.");

        if (task.Status == TaskNewStatus.COMPLETED)
            return BadRequest("Pabeigtam Task darbinieku mainīt nedrīkst.");

        var employeeExists = await _db.Employees.AnyAsync(x =>
            x.Id == dto.EmployeeId &&
            x.IsActive);

        if (!employeeExists)
            return BadRequest("Darbinieks nav atrasts vai nav aktīvs.");

        task.Employee_ID = dto.EmployeeId;
        task.Assigned_At = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{taskId:int}/status")]
    public async Task<IActionResult> ChangeStatus(
        int taskId,
        [FromBody] ChangeTaskNewStatusRequest dto)
    {
        if (taskId <= 0)
            return BadRequest("Task ID nav derīgs.");

        if (!Enum.IsDefined(typeof(TaskNewStatus), dto.Status))
            return BadRequest("Task statuss nav derīgs.");

        var newStatus = (TaskNewStatus)dto.Status;

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        var task = await _db.TasksNew
            .Include(x => x.ProductionExecution)
                .ThenInclude(x => x!.ProductionBatchTopPart)
            .FirstOrDefaultAsync(x =>
                x.ID == (uint)taskId &&
                x.IsActive);

        if (task == null)
            return NotFound("Task nav atrasts.");

        var transitionAllowed = task.Status switch
        {
            TaskNewStatus.WAITING =>
                newStatus == TaskNewStatus.STARTED,

            TaskNewStatus.STARTED =>
                newStatus == TaskNewStatus.PAUSED ||
                newStatus == TaskNewStatus.COMPLETED,

            TaskNewStatus.PAUSED =>
                newStatus == TaskNewStatus.STARTED,

            _ => false
        };

        if (!transitionAllowed)
            return BadRequest(
                $"Statusa pāreja {task.Status} → {newStatus} nav atļauta.");

        if (newStatus == TaskNewStatus.STARTED &&
            !task.Employee_ID.HasValue)
        {
            return BadRequest(
                "Pirms Task sākšanas tam jāpiešķir darbinieks.");
        }

        if (newStatus == TaskNewStatus.STARTED)
            {
                var hasIncompleteDependencies = await (
                    from dependency in _db.TaskNewDependencies
                    join previousTask in _db.TasksNew
                        on dependency.DependsOnTaskNew_ID equals previousTask.ID
                    where dependency.TaskNew_ID == task.ID &&
                        (
                            !previousTask.IsActive ||
                            previousTask.Status != TaskNewStatus.COMPLETED
                        )
                    select dependency.ID
                ).AnyAsync();

                if (hasIncompleteDependencies)
                {
                    return BadRequest(
                        "Task nevar sākt, kamēr nav pabeigti visi iepriekšējie procesi.");
                }
                
            }
        
        if (newStatus == TaskNewStatus.STARTED)
            {
                var hasIncompleteComponents = await (
                        from processComponent in
                            _db.WorkflowProcessComponents.AsNoTracking()

                        join workflowComponent in
                            _db.WorkflowComponents.AsNoTracking()
                            on processComponent.WorkflowComponentId
                            equals workflowComponent.Id

                        where
                            processComponent.ProcessNodeId ==
                                task.WorkflowNode_ID &&
                            processComponent.RequiresStaging &&
                            workflowComponent.IsActive &&
                            workflowComponent.ComponentType == 1 &&
                            !_db.ProductionComponentStagings.Any(staging =>
                                staging.ProductionExecution_ID ==
                                    task.ProductionExecution_ID &&
                                staging.WorkflowProcessComponent_ID ==
                                    processComponent.Id &&
                                staging.IsActive &&
                                staging.StagedQuantity >= staging.RequiredQuantity)

                        select processComponent.Id
                    ).AnyAsync();

                if (hasIncompleteComponents)
                {
                    return BadRequest(
                        "Task nevar sākt, kamēr nav sakomplektētas visas procesa komponentes.");
                }
            }

        if (dto.Comment?.Trim().Length > 500)
            return BadRequest(
                "Komentārs nedrīkst pārsniegt 500 rakstzīmes.");

        if (dto.ChangedByEmployeeId.HasValue)
        {
            var employeeExists = await _db.Employees.AnyAsync(x =>
                x.Id == dto.ChangedByEmployeeId.Value &&
                x.IsActive);

            if (!employeeExists)
                return BadRequest(
                    "Statusa mainītājs nav atrasts vai nav aktīvs.");
        }

        if (task.ProductionExecution == null ||
            !task.ProductionExecution.IsActive)
        {
            return BadRequest(
                "Task ražošanas izpildes vienība nav aktīva.");
        }

        if (task.ProductionExecution.Status ==
                ProductionExecutionStatus.COMPLETED ||
            task.ProductionExecution.Status ==
                ProductionExecutionStatus.SCRAPPED)
        {
            return BadRequest(
                "Pabeigtas vai norakstītas izpildes vienības Task mainīt nedrīkst.");
        }

        if (newStatus == TaskNewStatus.STARTED &&
                task.Status == TaskNewStatus.WAITING)
            {
                var consumptionError =
                    await _productionRequirementService
                        .ConsumeForTaskAsync(task);

                if (consumptionError != null)
                    return BadRequest(consumptionError);
            }
        
        if (newStatus == TaskNewStatus.COMPLETED)
            {
                var productionError =
                    await _productionRequirementService
                        .ProduceTaskOutputsAsync(task);

                if (productionError != null)
                    return BadRequest(productionError);
            }

        var previousStatus = task.Status;
        var now = DateTime.UtcNow;

        task.Status = newStatus;

        switch (newStatus)
        {
            case TaskNewStatus.STARTED:
                task.Started_At ??= now;
                task.Paused_At = null;

                if (task.ProductionExecution.Status ==
                    ProductionExecutionStatus.WAITING)
                {
                    task.ProductionExecution.Status =
                        ProductionExecutionStatus.IN_PRODUCTION;

                    task.ProductionExecution.Started_At ??= now;
                }

                break;

            case TaskNewStatus.PAUSED:
                task.Paused_At = now;
                break;

            case TaskNewStatus.COMPLETED:
                task.Completed_At = now;
                task.Paused_At = null;
                break;
        }

        if (newStatus == TaskNewStatus.COMPLETED)
            {
                var hasOtherIncompleteTasks = await _db.TasksNew
                    .AnyAsync(x =>
                        x.ProductionExecution_ID == task.ProductionExecution_ID &&
                        x.ID != task.ID &&
                        x.IsActive &&
                        x.Status != TaskNewStatus.COMPLETED);

                if (!hasOtherIncompleteTasks &&
                    task.ProductionExecution.Status !=
                        ProductionExecutionStatus.COMPLETED)
                {
                    task.ProductionExecution.Status =
                        ProductionExecutionStatus.COMPLETED;

                    task.ProductionExecution.Completed_At = now;

                    if (task.ProductionExecution.ProductionBatchTopPart_ID.HasValue &&
                        task.ProductionExecution.ProductionBatchTopPart != null)
                    {
                        task.ProductionExecution.ProductionBatchTopPart.Done_Qty +=
                            (uint)task.ProductionExecution.Quantity;
                    }
                }
            }

        _db.TaskNewStatusHistories.Add(new TaskNewStatusHistory
        {
            TaskNew_ID = task.ID,
            FromStatus = previousStatus,
            ToStatus = newStatus,
            ChangedByEmployee_ID = dto.ChangedByEmployeeId,
            Changed_At = now,
            Comment = dto.Comment?.Trim()
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return NoContent();
    }

    [HttpGet]
        public async Task<ActionResult<List<TaskNewListItemDto>>> GetAll()
        {
            var tasks = await _db.TasksNew
                .AsNoTracking()
                .Include(x => x.WorkflowNode)
                .Include(x => x.Employee)
                .Include(x => x.WorkCenter)
                .Where(x => x.IsActive)
                .OrderBy(x => x.Status)
                .ThenBy(x => x.Created_At)
                .ToListAsync();
            
            var taskIds = tasks
                .Select(x => x.ID)
                .ToList();

            var blockedTaskIds = (await (
                from dependency in _db.TaskNewDependencies.AsNoTracking()
                join previousTask in _db.TasksNew.AsNoTracking()
                    on dependency.DependsOnTaskNew_ID equals previousTask.ID
                where taskIds.Contains(dependency.TaskNew_ID) &&
                    (
                        !previousTask.IsActive ||
                        previousTask.Status != TaskNewStatus.COMPLETED
                    )
                select dependency.TaskNew_ID
            )
            .Distinct()
            .ToListAsync())
            .ToHashSet();
        
            var incompleteComponentTaskIds = (await (
                from task in _db.TasksNew.AsNoTracking()

                from processComponent in
                    _db.WorkflowProcessComponents.AsNoTracking()

                join workflowComponent in
                    _db.WorkflowComponents.AsNoTracking()
                    on processComponent.WorkflowComponentId
                    equals workflowComponent.Id

                where
                    taskIds.Contains(task.ID) &&
                    processComponent.ProcessNodeId ==
                        task.WorkflowNode_ID &&
                    processComponent.RequiresStaging &&
                    workflowComponent.IsActive &&
                    workflowComponent.ComponentType == 1 &&
                    !_db.ProductionComponentStagings.Any(staging =>
                        staging.ProductionExecution_ID ==
                            task.ProductionExecution_ID &&
                        staging.WorkflowProcessComponent_ID ==
                            processComponent.Id &&
                        staging.IsActive &&
                        staging.StagedQuantity >= staging.RequiredQuantity)

                select task.ID
            )
            .Distinct()
            .ToListAsync())
            .ToHashSet();

            var rows = tasks
                .Select(x => new TaskNewListItemDto
                {
                    Id = x.ID,
                    ProductionExecutionId = x.ProductionExecution_ID,
                    WorkflowNodeId = x.WorkflowNode_ID,
                    ProcessName = x.WorkflowNode?.Name ?? "",
                    EmployeeId = x.Employee_ID,
                    EmployeeName = x.Employee?.EmployeeName ?? "",
                    WorkCenterId = x.WorkCenter_ID,
                    WorkCenterName = x.WorkCenter?.WorkCentr_Name ?? "",
                    Quantity = x.Quantity,
                    Status = (int)x.Status,
                    CanStart =
                        (
                            x.Status == TaskNewStatus.WAITING ||
                            x.Status == TaskNewStatus.PAUSED
                        ) &&
                        !blockedTaskIds.Contains(x.ID) &&
                        !incompleteComponentTaskIds.Contains(x.ID),
                    CreatedAt = x.Created_At,
                    StartedAt = x.Started_At,
                    PausedAt = x.Paused_At,
                    CompletedAt = x.Completed_At
                })
                .ToList();

            return Ok(rows);
        }

    [HttpGet("{taskId:int}")]
        public async Task<ActionResult<TaskNewDetailsDto>> GetById(int taskId)
        {
            if (taskId <= 0)
                return BadRequest("Task ID nav derīgs.");

            var row = await _db.TasksNew
                .AsNoTracking()
                .Where(x =>
                    x.ID == (uint)taskId &&
                    x.IsActive)
                .Select(x => new TaskNewDetailsDto
                {
                    Id = x.ID,
                    ProductionExecutionId = x.ProductionExecution_ID,
                    WorkflowNodeId = x.WorkflowNode_ID,
                    ProcessName = x.WorkflowNode != null
                        ? x.WorkflowNode.Name ?? ""
                        : "",
                    EmployeeId = x.Employee_ID,
                    EmployeeName = x.Employee != null
                        ? x.Employee.EmployeeName
                        : "",
                    WorkCenterId = x.WorkCenter_ID,
                    WorkCenterName = x.WorkCenter != null
                        ? x.WorkCenter.WorkCentr_Name
                        : "",
                    Quantity = x.Quantity,
                    Status = x.Status == TaskNewStatus.WAITING ? 1
                        : x.Status == TaskNewStatus.STARTED ? 2
                        : x.Status == TaskNewStatus.PAUSED ? 3
                        : x.Status == TaskNewStatus.COMPLETED ? 4
                        : 0,
                    CreatedAt = x.Created_At,
                    StartedAt = x.Started_At,
                    PausedAt = x.Paused_At,
                    CompletedAt = x.Completed_At,

                    TopPartId = x.ProductionExecution != null
                        ? x.ProductionExecution.TopPart_ID
                        : 0,
                    TopPartCode =
                        x.ProductionExecution != null &&
                        x.ProductionExecution.TopPart != null
                            ? x.ProductionExecution.TopPart.TopPartCode
                            : "",
                    TopPartName =
                        x.ProductionExecution != null &&
                        x.ProductionExecution.TopPart != null
                            ? x.ProductionExecution.TopPart.TopPartName
                            : "",
                    WorkflowId = x.ProductionExecution != null
                        ? x.ProductionExecution.Workflow_ID
                        : 0,
                    WorkflowVersion =
                        x.ProductionExecution != null &&
                        x.ProductionExecution.Workflow != null
                            ? x.ProductionExecution.Workflow.WorkflowVersion
                            : 0
                })
                .FirstOrDefaultAsync();

            if (row == null)
                return NotFound("Task nav atrasts.");
            
            var hasIncompleteDependencies = await (
                from dependency in _db.TaskNewDependencies.AsNoTracking()
                join previousTask in _db.TasksNew.AsNoTracking()
                    on dependency.DependsOnTaskNew_ID equals previousTask.ID
                where dependency.TaskNew_ID == row.Id &&
                    (
                        !previousTask.IsActive ||
                        previousTask.Status != TaskNewStatus.COMPLETED
                    )
                select dependency.ID
            ).AnyAsync();

            var hasIncompleteComponents = await (
                from processComponent in
                    _db.WorkflowProcessComponents.AsNoTracking()

                join workflowComponent in
                    _db.WorkflowComponents.AsNoTracking()
                    on processComponent.WorkflowComponentId
                    equals workflowComponent.Id

                where
                    processComponent.ProcessNodeId ==
                        row.WorkflowNodeId &&
                    processComponent.RequiresStaging &&
                    workflowComponent.IsActive &&
                    workflowComponent.ComponentType == 1 &&
                    !_db.ProductionComponentStagings.Any(staging =>
                        staging.ProductionExecution_ID ==
                            row.ProductionExecutionId &&
                        staging.WorkflowProcessComponent_ID ==
                            processComponent.Id &&
                        staging.IsActive &&
                        staging.StagedQuantity >= staging.RequiredQuantity)

                select processComponent.Id
            ).AnyAsync();

            row.CanStart =
                (
                    row.Status == (int)TaskNewStatus.WAITING ||
                    row.Status == (int)TaskNewStatus.PAUSED
                ) &&
                !hasIncompleteDependencies &&
                !hasIncompleteComponents;

            row.StatusHistory = await _db.TaskNewStatusHistories
                .AsNoTracking()
                .Where(x => x.TaskNew_ID == row.Id)
                .OrderBy(x => x.Changed_At)
                .Select(x => new TaskNewStatusHistoryDto
                {
                    Id = x.ID,
                    FromStatus = x.FromStatus == null ? (int?)null
                        : x.FromStatus == TaskNewStatus.WAITING ? 1
                        : x.FromStatus == TaskNewStatus.STARTED ? 2
                        : x.FromStatus == TaskNewStatus.PAUSED ? 3
                        : x.FromStatus == TaskNewStatus.COMPLETED ? 4
                        : 0,

                    ToStatus = x.ToStatus == TaskNewStatus.WAITING ? 1
                        : x.ToStatus == TaskNewStatus.STARTED ? 2
                        : x.ToStatus == TaskNewStatus.PAUSED ? 3
                        : x.ToStatus == TaskNewStatus.COMPLETED ? 4
                        : 0,
                    ChangedByEmployeeId = x.ChangedByEmployee_ID,
                    ChangedByEmployeeName = x.ChangedByEmployee != null
                        ? x.ChangedByEmployee.EmployeeName
                        : "",
                    ChangedAt = x.Changed_At,
                    Comment = x.Comment
                })
                .ToListAsync();

            return Ok(row);
        }

        [HttpPost("{taskId:int}/split")]
        public async Task<IActionResult> Split(
            int taskId,
            [FromBody] SplitTaskNewRequest dto)
        {
            if (taskId <= 0)
                return BadRequest("Task ID nav derīgs.");

            if (dto.Parts == null || dto.Parts.Count < 2)
                return BadRequest("Task jāsadala vismaz divās daļās.");

            if (dto.Parts.Any(x => x.Quantity <= 0))
                return BadRequest(
                    "Katras Task daļas daudzumam jābūt lielākam par 0.");

            var employeeIds = dto.Parts
                .Where(x => x.EmployeeId.HasValue)
                .Select(x => x.EmployeeId!.Value)
                .Distinct()
                .ToList();

            var validEmployeeIds = await _db.Employees
                .Where(x =>
                    employeeIds.Contains(x.Id) &&
                    x.IsActive)
                .Select(x => x.Id)
                .ToListAsync();

            if (employeeIds.Any(id => !validEmployeeIds.Contains(id)))
                return BadRequest(
                    "Viens vai vairāki darbinieki nav atrasti vai nav aktīvi.");

            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            var task = await _db.TasksNew
                .FromSqlInterpolated(
                    $"SELECT * FROM tasks_new WHERE ID = {(uint)taskId} FOR UPDATE")
                .FirstOrDefaultAsync();

            if (task == null || !task.IsActive)
                return NotFound("Task nav atrasts.");

            if (task.Status != TaskNewStatus.WAITING)
                return BadRequest("Sadalīt drīkst tikai WAITING Task.");
            
            var executionTasks = await _db.TasksNew
                .Where(x =>
                    x.ProductionExecution_ID == task.ProductionExecution_ID &&
                    x.IsActive)
                .ToListAsync();

            if (executionTasks.Any(x =>
                    x.Status != TaskNewStatus.WAITING))
            {
                return BadRequest(
                    "Sadalīt drīkst tikai pilnībā nesāktu ražošanas izpildi.");
            }

            var selectedTaskHasIncomingDependency =
                await _db.TaskNewDependencies.AnyAsync(x =>
                    x.TaskNew_ID == task.ID);

            if (selectedTaskHasIncomingDependency)
            {
                return BadRequest(
                    "Ražošanas apjomu drīkst sadalīt tikai pirmajā Flow procesā.");
            }

            var executionAlreadySplit = executionTasks
                .GroupBy(x => x.WorkflowNode_ID)
                .Any(group => group.Count() > 1);

            if (executionAlreadySplit)
            {
                return BadRequest(
                    "Šī ražošanas izpilde jau ir sadalīta.");
            }

            if (dto.Parts.Sum(x => x.Quantity) != task.Quantity)
                return BadRequest(
                    "Task daļu daudzumu summai jāsakrīt ar sākotnējo daudzumu.");
            
            var executionTaskIds = executionTasks
                .Select(x => x.ID)
                .ToList();

            var executionDependencies = await _db.TaskNewDependencies
                .AsNoTracking()
                .Where(x =>
                    executionTaskIds.Contains(x.TaskNew_ID) &&
                    executionTaskIds.Contains(x.DependsOnTaskNew_ID))
                .ToListAsync();

            var now = DateTime.UtcNow;
            var firstPart = dto.Parts[0];

            foreach (var executionTask in executionTasks)
                executionTask.Quantity = firstPart.Quantity;

            task.Employee_ID = firstPart.EmployeeId;
            task.Assigned_At = firstPart.EmployeeId.HasValue
                ? now
                : null;

            foreach (var part in dto.Parts.Skip(1))
            {
                var cloneByOriginalTaskId =
                    new Dictionary<uint, TaskNew>();

                foreach (var originalTask in executionTasks)
                {
                    var employeeId = originalTask.ID == task.ID
                        ? part.EmployeeId
                        : originalTask.Employee_ID;

                    var clonedTask = new TaskNew
                    {
                        ProductionExecution_ID =
                            originalTask.ProductionExecution_ID,
                        WorkflowNode_ID = originalTask.WorkflowNode_ID,
                        Employee_ID = employeeId,
                        WorkCenter_ID = originalTask.WorkCenter_ID,
                        Quantity = part.Quantity,
                        Status = TaskNewStatus.WAITING,
                        Created_At = now,
                        Assigned_At = employeeId.HasValue ? now : null,
                        IsActive = true
                    };

                    cloneByOriginalTaskId[originalTask.ID] = clonedTask;
                    _db.TasksNew.Add(clonedTask);
                }

                await _db.SaveChangesAsync();

                foreach (var dependency in executionDependencies)
                {
                    _db.TaskNewDependencies.Add(
                        new TaskNewDependency
                        {
                            TaskNew_ID =
                                cloneByOriginalTaskId[dependency.TaskNew_ID].ID,
                            DependsOnTaskNew_ID =
                                cloneByOriginalTaskId[
                                    dependency.DependsOnTaskNew_ID].ID
                        });
                }

                _db.TaskNewStatusHistories.AddRange(
                    cloneByOriginalTaskId.Values.Select(clonedTask =>
                        new TaskNewStatusHistory
                        {
                            TaskNew_ID = clonedTask.ID,
                            FromStatus = null,
                            ToStatus = TaskNewStatus.WAITING,
                            Changed_At = now,
                            Comment =
                                "Task izveidots ražošanas izpildes sadalīšanas rezultātā."
                        }));
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }

        [HttpGet("executions/{executionId:int}")]
        public async Task<IActionResult> GetExecution(int executionId)
            {
                if (executionId <= 0)
                    return BadRequest("ProductionExecution ID nav derīgs.");

                var row = await _db.ProductionExecutions
                    .AsNoTracking()
                    .Where(x =>
                        x.ID == (uint)executionId &&
                        x.IsActive)
                    .Select(x => new
                    {
                        x.ID,
                        Status = x.Status == ProductionExecutionStatus.WAITING ? 1
                            : x.Status == ProductionExecutionStatus.IN_PRODUCTION ? 2
                            : x.Status == ProductionExecutionStatus.COMPLETED ? 3
                            : x.Status == ProductionExecutionStatus.SCRAPPED ? 4
                            : 0,
                        x.Quantity,
                        x.Completed_At,
                        x.ProductionBatchTopPart_ID,
                        Done_Qty = x.ProductionBatchTopPart != null
                            ? (uint?)x.ProductionBatchTopPart.Done_Qty
                            : null
                    })
                    .FirstOrDefaultAsync();

                if (row == null)
                    return NotFound("ProductionExecution nav atrasts.");

                return Ok(row);
            }

    }
}
