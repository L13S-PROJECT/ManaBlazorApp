using ManaApp.Shared.DTOs.Tasks;
using ManiApi.Data;
using Microsoft.AspNetCore.Mvc;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/tasks-new")]
    public class TasksNewController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TasksNewController(AppDbContext db)
        {
            _db = db;
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

        var task = await _db.TasksNew
            .Include(x => x.ProductionExecution)
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

            if (dto.Parts.Sum(x => x.Quantity) != task.Quantity)
                return BadRequest(
                    "Task daļu daudzumu summai jāsakrīt ar sākotnējo daudzumu.");

            var now = DateTime.UtcNow;
            var firstPart = dto.Parts[0];

            task.Quantity = firstPart.Quantity;
            task.Employee_ID = firstPart.EmployeeId;
            task.Assigned_At = firstPart.EmployeeId.HasValue
                ? now
                : null;

            foreach (var part in dto.Parts.Skip(1))
            {
                var newTask = new TaskNew
                {
                    ProductionExecution_ID = task.ProductionExecution_ID,
                    WorkflowNode_ID = task.WorkflowNode_ID,
                    Employee_ID = part.EmployeeId,
                    WorkCenter_ID = task.WorkCenter_ID,
                    Quantity = part.Quantity,
                    Status = TaskNewStatus.WAITING,
                    Created_At = now,
                    Assigned_At = part.EmployeeId.HasValue ? now : null,
                    IsActive = true
                };

                _db.TasksNew.Add(newTask);
                await _db.SaveChangesAsync();

                _db.TaskNewStatusHistories.Add(new TaskNewStatusHistory
                {
                    TaskNew_ID = newTask.ID,
                    FromStatus = null,
                    ToStatus = TaskNewStatus.WAITING,
                    Changed_At = now,
                    Comment = "Task izveidots sadalīšanas rezultātā."
                });
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }

    }
}