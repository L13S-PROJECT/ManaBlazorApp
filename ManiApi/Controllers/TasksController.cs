// Šis kontrolieris ir paredzēts ražošanas uzdevumu (tasks) pārvaldībai: uzdevumu saraksta skatīšanai, uzdevuma pieprasīšanai (claim) un pabeigšanai (finish).

using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ManiApi.Models;
using System.Data;
using ManiApi.Services;
using ManiApi.DTOs.Tasks;
using ManiApi.Services.Detail;
using ManiApi.Services.Finishing;
using ManiApi.Services.Assembly;
using ManiApi.Services.Inline;
using TaskRowDto = ManiApi.DTOs.Tasks.TaskRowDto;
using ManiApi.Services.Tasks;
using ManiApi.Services.ProductionFlows.ParentSeparate;


namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TaskService _taskService;
    private readonly DetailTasksService _detailService;
    private readonly AssemblyTasksService _assemblyTasksService;
    private readonly ManiApi.Services.ProductionFlows.ParentSeparate.ParentSeparateAssemblyService _parentSeparateAssemblyService;
    private readonly TaskManagementService _taskManagementService;
    private readonly FinishingFlowService _finishingFlowService;
    private readonly FinishingTasksService _finishingTasksService;
    private readonly TaskQueryService _taskQueryService;
    private readonly InlineFinishingTasksService _inlineFinishingTasksService;
    private readonly InlineAssemblyTasksService _inlineAssemblyTasksService;
    private readonly ParentSeparateFinishingService _parentSeparateFinishingService;
    
    public TasksController(
        AppDbContext db,
        TaskService taskService,
        DetailTasksService detailService,
        AssemblyTasksService assemblyTasksService,
        TaskManagementService taskManagementService,
        FinishingFlowService finishingFlowService,
        FinishingTasksService finishingTasksService,
        TaskQueryService taskQueryService,
        InlineFinishingTasksService inlineFinishingTasksService,
        InlineAssemblyTasksService inlineAssemblyTasksService,
        ManiApi.Services.ProductionFlows.ParentSeparate.ParentSeparateAssemblyService parentSeparateAssemblyService,
        ManiApi.Services.ProductionFlows.ParentSeparate.ParentSeparateFinishingService parentSeparateFinishingService
)
    {
        _db = db;
        _taskService = taskService;
        _detailService = detailService;
        _assemblyTasksService = assemblyTasksService;
        _taskManagementService = taskManagementService;
        _taskQueryService = taskQueryService;
        _finishingFlowService = finishingFlowService;
        _finishingTasksService = finishingTasksService;
        _finishingFlowService = finishingFlowService;
        _inlineFinishingTasksService = inlineFinishingTasksService;
        _inlineAssemblyTasksService = inlineAssemblyTasksService;
        _parentSeparateAssemblyService = parentSeparateAssemblyService;
        _parentSeparateFinishingService = parentSeparateFinishingService;
    }

        // GET: /api/tasks/for-employee?empId=101
// Rāda: Prioritārie (Tasks_Priority=1) ar statusu 1 (nav iesākts) + paša iesāktie (statuss=2)
// GET: /api/tasks/for-employee?empId=101


[HttpGet("for-employee")]
public async Task<IActionResult> GetForEmployee(
    [FromQuery] int empId = 1,
    [FromQuery] int workcentrId = 0
)
{
    return Ok(await _taskService.GetForEmployee(empId));
}


        // POST: /api/tasks/claim   body: { "taskId": 123, "empId": 101 }
// Atzīmē “SĀKT”: aizliedz, ja darbiniekam jau ir kāds status=2.
[HttpPost("claim")]
public async Task<IActionResult> Claim([FromBody] ClaimDto dto)
{
   var result = await _taskService.ClaimTask(dto.TaskId, dto.EmpId);

if (!result.Success)
    return Conflict(result.Error);

return Ok(new { claimed = true });

}


/// POST: /api/tasks/finish   body: { "taskId": 123, "qtyDoneAdd": 5 }
[HttpPost("finish")]
public async Task<IActionResult> Finish([FromBody] FinishDto dto)
{
    
     if (dto is null || dto.TaskId <= 0)
        return BadRequest("TaskId is required.");

    var result = await _taskService.FinishTask(dto.TaskId, dto.QtyDoneAdd);

    if (result.Success)
        {
            // te vēlāk pieslēgsim ProductionFlow

        }

    if (!result.Success)
        return Conflict(result.Error);

    return Ok(new { finished = true });

}
     

// POST: /api/tasks/update-steps
// Body: [ { "taskId": 123, "tasks_Priority": true, "assigned_To": 101 }, ... ]

[HttpPost("update-steps")]
public async Task<IActionResult> UpdateSteps([FromBody] List<UpdateStepDto> steps)
{
    var updated = await _taskManagementService.UpdateSteps(steps);
    return Ok(new { updated });
}



// POST: /api/tasks/activate-part
// Maina uz statusu 1 TIKAI šai partijai + detaļai, un tikai no 5.
[HttpPost("activate-part")]
public async Task<IActionResult> ActivatePart([FromBody] ActivatePartDto dto)
{
    var updated = await _taskManagementService.ActivatePart(dto);
return Ok(new { updated });

}


// Ko atdodam atpakaļ Blazoram


[HttpPost("open-finishing")]
public async Task<IActionResult> OpenFinishing([FromBody] OpenFinishingDto dto)
{
Console.WriteLine(
 $"[open-finishing] bpId={dto.BatchProductId}, ptpId={dto.ProductToPartId}, qty={dto.Qty}, ral={dto.RalColorId}, comment='{dto.Comment}'");

var result = await _finishingFlowService.OpenFinishing(dto);
return Ok(result);

}

// GET: /api/tasks/active-parts?batchId=123
// Atgriež ProductToPart_ID sarakstu šai partijai ar statusu 1
[HttpGet("active-parts")]
public async Task<IActionResult> GetActiveParts([FromQuery] int batchProductId)
{
    var list = await _taskManagementService.GetActiveParts(batchProductId);

    return Ok(list);
}


// GET: /api/tasks/detailed-summary-by-batch?batchId=123
[HttpGet("detailed-summary-by-batch")]
public async Task<IActionResult> GetDetailedSummaryByBatch([FromQuery] int batchId)
{
    if (batchId <= 0)
        return BadRequest("batchId is required.");

    var list = await _taskQueryService.GetDetailedSummaryByBatch(batchId);
return Ok(list);
}

[HttpGet("finishing-waves")]
public async Task<IActionResult> GetFinishingWaves([FromQuery] int batchProductId, [FromQuery] int productToPartId)
{
    if (batchProductId <= 0 || productToPartId <= 0)
        return BadRequest("batchProductId and productToPartId are required.");

  var list = await _taskQueryService.GetFinishingWaves(batchProductId, productToPartId);
        return Ok(list);
}

[HttpGet("finishing-waves-child")]
public async Task<IActionResult> GetFinishingWavesChild(
    int batchProductId,
    int productToPartId)
{
    var result = await _taskQueryService.GetFinishingWavesChild(
    batchProductId,
    productToPartId);

    return Ok(result);
}


// GET: /api/tasks/detailed-summary-by-batchproduct?batchProductId=123
[HttpGet("detailed-summary-by-batchproduct")]
public async Task<IActionResult> GetDetailedSummaryByBatchProduct([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var list = await _taskQueryService.GetDetailedSummaryByBatchProduct(batchProductId);
        return Ok(list);
}


[HttpGet("finishing-inprogress-by-version")]
public async Task<IActionResult> GetFinishingInProgressByVersion([FromQuery] int versionId)
{
    var val = await _taskQueryService.GetFinishingInProgressByVersion(versionId);
return Ok(new { finishingInProgress = val });

}


[HttpGet("finishing-allocated-by-version")]
public async Task<IActionResult> GetFinishingAllocatedByVersion([FromQuery] int versionId)
{
    var val = await _taskQueryService.GetFinishingAllocatedByVersion(versionId);
        return Ok(new { finishingAllocated = val });
}


[HttpPost("update-finishing-qty")]
public async Task<IActionResult> UpdateFinishingQty([FromBody] UpdateFinishingQtyDto dto)
{
   
   Console.WriteLine($"[update-finishing-qty] taskId={dto.TaskId} qty={dto.Qty} comment='{dto.Comment}'");

   
    if (dto is null || dto.TaskId <= 0 || dto.Qty < 0)
        return BadRequest("TaskId un Qty ir obligāti (Qty >= 0).");

    var t = await _db.Tasks.FirstOrDefaultAsync(x => x.ID == dto.TaskId && x.IsActive);
    if (t is null) return NotFound();

    // ja jau sācies vai nav “atvērts” (status=1), labot nedrīkst
    if (t.Started_At != null || t.Tasks_Status != 1)
        return BadRequest("Task already started (vai nav status=1).");

    t.Qty_Done = dto.Qty;
    t.Tasks_Comment = dto.Comment;

    await _db.SaveChangesAsync();
    return Ok(new { updated = true, taskId = t.ID, qty = t.Qty_Done });
}

// GET: /api/tasks/detailed-indicators?batchProductId=123
[HttpGet("detailed-indicators")]
public async Task<IActionResult> GetDetailedIndicators([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var list = await _taskQueryService.GetDetailedIndicators(batchProductId);
        return Ok(list);
}

// GET: /api/tasks/assembly-indicators?batchProductId=123
[HttpGet("assembly-indicators")]
public async Task<IActionResult> GetAssemblyIndicators([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var list = await _taskQueryService.GetAssemblyIndicators(batchProductId);
        return Ok(list);
}


// GET: /api/tasks/finishing-indicators?batchProductId=123
[HttpGet("finishing-indicators")]
public async Task<IActionResult> GetFinishingIndicators([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var list = await _taskQueryService.GetFinishingIndicators(batchProductId);
        return Ok(list);
}

[HttpPost("update-comment")]
public async Task<IActionResult> UpdateComment([FromBody] UpdateCommentDto dto)
{
    if (dto is null || dto.TaskId <= 0)
        return BadRequest("TaskId is required.");

    var updated = await _taskManagementService.UpdateComment(dto);

    if (updated == 0)
        return NotFound();

    return Ok(new { updated });
}



// piešķir konkrētam tasksam konkrētu darbinieku - Assigned_TO


[HttpPost("update-assignee")]
public async Task<IActionResult> UpdateAssignee([FromBody] UpdateTaskAssigneeDto dto)
{
    var updated = await _taskManagementService.UpdateAssignee(dto);

    return Ok(new { updated });
}

// GET: /api/tasks/by-step?batchProductId=123&topPartStepId=456
[HttpGet("by-step")]
public async Task<IActionResult> GetByStep(
    [FromQuery] int batchProductId,
    [FromQuery] int topPartStepId
)
{
    if (batchProductId <= 0 || topPartStepId <= 0)
        return BadRequest("batchProductId and topPartStepId are required.");

    var list = await _taskQueryService.GetByStep(batchProductId, topPartStepId);
        return Ok(list);
}

// GET: /api/tasks/by-batch?batchProductId=123&stepType=1
[HttpGet("by-batch")]
public async Task<IActionResult> GetTasksByBatch(
    [FromQuery] int batchProductId,
    [FromQuery] int stepType)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var list = await _taskQueryService.GetTasksByBatch(batchProductId, stepType);
        return Ok(list);
}

[HttpPost("update-comment-visibility")]
public async Task<IActionResult> UpdateCommentVisibility([FromBody] UpdateCommentVisibilityDto dto)
{
    var t = await _db.Tasks.FirstOrDefaultAsync(x => x.ID == dto.TaskId && x.IsActive);
    if (t is null) return NotFound();

    t.Is_Comment_For_Employee = dto.IsCommentForEmployee;
    await _db.SaveChangesAsync();

    return Ok();
}

// GET: /api/tasks/employee-load?empId=123 - 13.02.2026
[HttpGet("employee-load")]
public async Task<IActionResult> GetEmployeeLoad([FromQuery] int empId)
{
    if (empId <= 0)
        return BadRequest("empId is required.");

    var header = await _taskQueryService.GetEmployeeHeader(empId);
        if (header.EmployeeName == null && header.WorkCentrTypeID == null)
        return NotFound("Employee not found.");

    var employeeName = header.EmployeeName;
    var workCenterName = header.WorkCenterName;
    var employeeWorkCenterId = header.WorkCentrTypeID;

var list = await _taskQueryService.GetEmployeeInProgress(empId);

// PRIORITĀRIE (status = 1, batch priority = true)

var priorityList = await _taskQueryService.GetEmployeePriority(empId, employeeWorkCenterId);

// SECĪGIE (status = 1, batch priority = false)


var normalList = await _taskQueryService.GetEmployeeNormal(empId, employeeWorkCenterId);

return Ok(new EmployeeLoadDto
{
    EmployeeName = employeeName,
    WorkCenterName = workCenterName,
    WorkCentrTypeID = employeeWorkCenterId,
    InProgress = list,
    Priority = priorityList,
    Normal = normalList
});

}

// GET: /api/tasks/steps-for-part?batchProductId=123&productToPartId=8
[HttpGet("steps-for-part")]
public async Task<IActionResult> GetStepsForPart(
    int batchProductId,
    int productToPartId,
    bool onlyUnassigned = false)
{
    var list = await (
        from t in _db.Tasks
        join ts in _db.TopPartSteps
            on t.TopPartStep_ID equals ts.Id

        join ea in _db.Employees
            on t.Assigned_To equals ea.Id into eaJoin
        from ea in eaJoin.DefaultIfEmpty()

        join ec in _db.Employees
            on t.Claimed_By equals ec.Id into ecJoin
        from ec in ecJoin.DefaultIfEmpty()

        where t.IsActive
      && t.BatchProduct_ID == batchProductId
      && ts.ProductToPartId == productToPartId

        orderby ts.StepOrder

        select new
        {
            TaskId = t.ID,
            StepOrder = ts.StepOrder,
            StepName = ts.StepName,
            Status = t.Tasks_Status,

            AssignedName = ea != null ? ea.EmployeeName : null,
            ClaimedName = ec != null ? ec.EmployeeName : null
        }
    ).ToListAsync();

    return Ok(list);
}

// GET: /api/tasks/unassigned
[HttpGet("unassigned")]
public async Task<IActionResult> GetUnassignedTasks()
{
   return Ok(await _taskQueryService.GetUnassignedTasks());
}

[HttpGet("workcenters")]
public async Task<IActionResult> GetWorkCenters()
{
    var list = await _taskQueryService.GetWorkCenters();
        return Ok(list);
}


// GET: /api/tasks/aggregated-by-batch?batchProductId=123&stepType=1

[HttpGet("aggregated-by-batch")]
public async Task<IActionResult> GetAggregatedByBatch(
    [FromQuery] int batchProductId,
    [FromQuery] int stepType)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    // TODO: šeit būs agregācijas loģika
    
var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
            SELECT
                t.ID,
                t.BatchProduct_ID,
                t.TopPartStep_ID,
                t.Tasks_Status,
                t.Assigned_To,
                t.Claimed_By,
                t.Started_At,
                t.Finished_At,
                t.Tasks_Comment,                 
                t.Is_Comment_For_Employee,       
                1 AS QtyDone,
                ts.Step_Name,
                tp.TopPart_Name,
                ts.ProductToPart_ID
            FROM tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
            JOIN toppart tp ON tp.ID = ptp.TopPart_ID
            WHERE t.IsActive = 1
                AND ts.Step_Type = @stepType
                AND t.BatchProduct_ID IN (
                    SELECT bp2.ID
                    FROM batches_products bp2
                    WHERE bp2.IsActive = 1
                    AND bp2.Batch_Id = (
                        SELECT bp0.Batch_Id
                        FROM batches_products bp0
                        WHERE bp0.ID = @bp
                    )
                    AND bp2.Version_Id = (
                        SELECT bp0.Version_Id
                        FROM batches_products bp0
                        WHERE bp0.ID = @bp
                    )
                );";

    cmd.Parameters.Add(new MySqlParameter("@bp", batchProductId));
    cmd.Parameters.Add(new MySqlParameter("@stepType", stepType));

        await using var cmdQty = conn.CreateCommand();
cmdQty.CommandText = @" 
SELECT 
    ptp.ID AS ProductToPartId,

    (
        SELECT bp2.Planned_Qty
        FROM batches_products bp2
        WHERE bp2.Batch_Id = (
            SELECT Batch_Id FROM batches_products WHERE ID = @bp LIMIT 1
        )
        AND bp2.Version_Id = (
            SELECT Version_Id FROM batches_products WHERE ID = @bp LIMIT 1
        )
        AND bp2.ProductToPart_ID IS NULL
        LIMIT 1
    ) AS ParentQty,

    SUM(
        CASE 
            WHEN bp.ProductToPart_ID = ptp.ID 
            THEN bp.Planned_Qty 
            ELSE 0 
        END
    ) AS ChildQty

FROM batches_products bp

JOIN producttopparts ptp 
    ON ptp.Version_ID = bp.Version_Id
    AND ptp.IsActive = 1

JOIN toppartsteps ts 
    ON ts.ProductToPart_ID = ptp.ID
    AND ts.IsActive = 1

JOIN stage_step_type_map m
    ON m.Step_Type_ID = ts.Step_Type
    AND m.Stage = 1
    AND m.IsActive = 1

WHERE bp.Batch_Id = (
    SELECT Batch_Id FROM batches_products WHERE ID = @bp LIMIT 1
)

GROUP BY ptp.ID;
";

cmdQty.Parameters.Add(new MySqlParameter("@bp", batchProductId));

var rawTasks = new List<RawTaskRow>();

await using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync())
{
    rawTasks.Add(new RawTaskRow
        {
            TaskId = r.GetInt32(0),
            BatchProductId = r.GetInt32(1),
            TopPartStepId = r.GetInt32(2),
            Status = r.GetInt32(3),
            Assigned_To = r.IsDBNull(4) ? (int?)null : r.GetInt32(4),
            Claimed_By = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
            StartedAt = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6),
            FinishedAt = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7),
            Comment = r.IsDBNull(8) ? null : r.GetString(8),
            IsCommentForEmployee = !r.IsDBNull(9) && r.GetBoolean(9),

            Qty = r.GetInt32(10),
            StepName = r.IsDBNull(11) ? null : r.GetString(11),
            TopPartName = r.IsDBNull(12) ? null : r.GetString(12),
            ProductToPartId = r.GetInt32(13),
        });
}

await r.DisposeAsync();

var qtyList = new List<DetailQtyRow>();

await using var r2 = await cmdQty.ExecuteReaderAsync();
while (await r2.ReadAsync())
{
    qtyList.Add(new DetailQtyRow
    {
        ProductToPartId = r2.GetInt32(0),
        ParentQty = r2.IsDBNull(1) ? 0 : r2.GetInt32(1),
        ChildQty = r2.IsDBNull(2) ? 0 : r2.GetInt32(2)
    });
}

var grouped = rawTasks
    .GroupBy(x => new 
        {
            x.ProductToPartId,
            x.StepName
        })
    .ToList();


var result = grouped
    .OrderBy(g => g.First().TopPartName)
    .ThenBy(g => g.First().StepName)
    .Select(g => new
{
    TopPartStepId = g.First().TopPartStepId,
    TotalQty =
        qtyList
            .Where(q => g.Select(x => x.ProductToPartId).Contains(q.ProductToPartId))
            .Select(q => q.ParentQty + q.ChildQty)
            .FirstOrDefault(),
    StepName = g.First().StepName,
    TopPartName = g.First().TopPartName,
    ProductToPartId = g.First().ProductToPartId,

    // pagaidām vienkārši – ņemam pirmo
    Assigned_To =
        g.Select(x => x.Assigned_To)
        .Distinct()
        .Count() == 1
            ? g.First().Assigned_To
            : null,
    Claimed_By =
        g.Select(x => x.Claimed_By)
        .Distinct()
        .Count() == 1
            ? g.First().Claimed_By
            : null,

    StartedAt =
        g.Where(x => x.StartedAt != null)
        .Min(x => x.StartedAt),
    FinishedAt =
        g.All(x => x.FinishedAt != null)
            ? g.Max(x => x.FinishedAt)
            : null,
    Comment = g.Select(x => x.Comment)
    .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),

    IsCommentForEmployee = g.Any(x => x.IsCommentForEmployee),

    Status =
    g.Any(x => x.Status == 2) ? 2 :
    g.All(x => x.Status == 3) ? 3 :
    g.All(x => x.Status == 1) ? 1 :
    5 // statuss gaida.
}).ToList();

    return Ok(result);
}

[HttpPost("update-assignee-aggregated")]
public async Task<IActionResult> UpdateAssigneeAggregated([FromBody] UpdateAssigneeAggregatedDto dto)
{
    if (dto is null || dto.BatchProductId <= 0 || dto.ProductToPartId <= 0)
        return BadRequest();

    var affected = await _taskManagementService.UpdateAssigneeAggregated(dto);

    return Ok(new { updated = affected });
}


[HttpPost("update-assignee-root")]
public async Task<IActionResult> UpdateAssigneeRoot([FromBody] UpdateAssigneeDto dto)
{
    if (dto is null || dto.BatchProductId <= 0)
        return BadRequest("BatchProductId is required.");

    var updated = await _taskManagementService.UpdateAssigneeRoot(dto);

    if (updated == 0)
        return NotFound();

    return Ok(new { updated });
}


[HttpPost("set-part-priority")]
public async Task<IActionResult> SetPartPriority([FromBody] SetPartPriorityDto dto)
{
    var updated = await _taskManagementService.SetPartPriority(dto);
return Ok(new { updated });

}



[HttpGet("ral-colors")]
public async Task<IActionResult> GetRalColors()
{
    var list = await _db.RalColors
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .Select(x => new
        {
            Id = x.ID,
            Name = x.Name
        })
        .ToListAsync();

    return Ok(list);
}

// GET: api/tasks/finishing-by-version-ral?versionId=3
[HttpGet("finishing-by-version-ral")]
public async Task<IActionResult> GetFinishingByVersionRal([FromQuery] int versionId)
{
    if (versionId <= 0)
        return BadRequest("versionId is required.");

        var result = await _db.Tasks
            .Join(_db.TopPartSteps,
                t => t.TopPartStep_ID,
                ts => ts.Id,
                (t, ts) => new { t, ts })
            .Join(_db.RalColors,
                x => x.t.RAL_Color_ID,
                rc => rc.ID,
                (x, rc) => new { x.t, x.ts, rc })
            .Where(x =>
                x.t.IsActive &&
                x.t.RAL_Color_ID != null &&
                x.ts.StepType == 3 &&
                _db.StockMovements.Any(sm =>
                    sm.BatchProduct_ID == x.t.BatchProduct_ID &&
                    sm.Version_ID == versionId))
            .GroupBy(x => new { x.t.RAL_Color_ID, x.rc.Name })
                .Select(g => new
                {
                    RalColorId = g.Key.RAL_Color_ID,
                    RalCode = g.Key.Name,
                    Qty = g.Sum(x => x.t.Qty_Done),
                    Status = g.Min(x => x.t.Tasks_Status)
                })
    .ToListAsync();

    return Ok(result);
}

// GET: api/stockmovements/assembly-available-ui?batchProductId=123
[HttpGet("assembly-available-ui")]
public async Task<IActionResult> GetAssemblyAvailableUi([FromQuery] int batchProductId)
{
    var val = await _parentSeparateFinishingService.GetAvailableAssemblyQty(batchProductId);
    return Ok(val);
    
}

[HttpGet("child-finishing-data")]
public async Task<IActionResult> GetChildFinishingData(
    [FromQuery] int batchProductId,
    [FromQuery] int productToPartId)
{
    if (batchProductId <= 0 || productToPartId <= 0)
        return BadRequest();

    var result = await _finishingTasksService.GetChildFinishingData(
        batchProductId,
        productToPartId);

    return Ok(new
    {
        IsPainting = result.isPainting,
        AvailableQty = result.availableQty
    });
}


[HttpPost("set-task-push")]
public async Task<IActionResult> SetTaskPush([FromBody] SetTaskPushDto dto)
{
    var updated = await _taskManagementService.SetTaskPush(dto);
        return Ok(new { updated });

}

[HttpGet("employee-load-v2")]
public async Task<IActionResult> GetEmployeeLoadV2(int empId = 0)
{
    var data = await GetEmployeeLoad(empId) as OkObjectResult;

    if (data == null)
        return BadRequest();

    dynamic? payload = data.Value;
    if (payload == null)
    return BadRequest();

var inProgress = payload.InProgress as IEnumerable<dynamic> ?? Enumerable.Empty<dynamic>();
var priority = payload.Priority as IEnumerable<dynamic> ?? Enumerable.Empty<dynamic>();
var normal = payload.Normal as IEnumerable<dynamic> ?? Enumerable.Empty<dynamic>();

var allTasks = inProgress
    .Concat(priority)
    .Concat(normal)
    .ToList();

var groups = allTasks
    .GroupBy(x => (int)x.RootId)
    .ToList();

var result = new List<EmployeeTaskRowV2>();

foreach (var g in groups)
{
    var items = g.ToList();

    var hasParent = items.Any(x => (int)x.BatchProductId == (int)x.RootId);
    var hasChild = items.Any(x => (int)x.BatchProductId != (int)x.RootId);
    var hasOnlyChild = !hasParent && hasChild;

    if (hasParent && !hasChild)
            {
                foreach (var t in items)
                {
                    result.Add(new EmployeeTaskRowV2
                    {
                        RootId = (int)t.RootId,
                        BatchCode = t.BatchCode,
                        BatchPriority = t.BatchPriority,
                        BatchProductId = (int)t.BatchProductId,
                        ProductToPartId = (int)t.ProductToPartId,
                        ProductName = t.ProductName,
                        TopPartName = t.TopPartName,
                        StepName = t.StepName,

                        DisplayQty = (int)t.Qty,
                        DisplayMinutes = (int)t.Qty * (int)t.EstimatedMinutes,

                        Status = (int)t.Status,
                        CanStart = t.CanStart,

                        Assigned_To = t.Assigned_To,
                        TopPartStepId = (int?)t.TopPartStepId ?? 0,
                        Tasks_Priority = t.Tasks_Priority,
                        Tasks_Push = t.Tasks_Push,

                        RowType = t.RowType,
                        ShowChildMark = false
                    });
                }

                continue;
            }

    if (hasParent && hasChild)
            {               
                var stepGroups = items
                    .GroupBy(x => new { x.ProductToPartId, x.StepOrder });

                foreach (var sg in stepGroups)
                {
                    var first = sg.First();

                    var parentQty = sg
                        .Where(x => (int)x.BatchProductId == (int)x.RootId)
                        .Sum(x => (int)x.Qty);

                    var childQty = sg
                        .Where(x => (int)x.BatchProductId != (int)x.RootId)
                        .Sum(x => (int)x.Qty);

                    var totalQty = parentQty + childQty;
                    var breakdown = childQty > 0 ? $"{parentQty}+{childQty}" : parentQty.ToString();
                    var totalMinutes = sg.Sum(x => (int)x.Qty * (int)x.EstimatedMinutes);

                    result.Add(new EmployeeTaskRowV2
                    {
                        RootId = (int)first.RootId,
                        BatchCode = first.BatchCode,
                        BatchProductId = (int)first.BatchProductId,
                        BatchPriority = first.BatchPriority,
                        ProductToPartId = (int)first.ProductToPartId,
                        ProductName = first.ProductName,
        
                        StepName = first.StepName,

                        DisplayQty = totalQty,
                        QtyBreakdown = breakdown,
                        DisplayMinutes = totalMinutes,
                        TopPartName = first.TopPartName,
                        Status = (int)first.Status,
                        CanStart = first.CanStart,

                        Assigned_To = first.Assigned_To,
                        TopPartStepId = (int?)first.TopPartStepId ?? 0,
                        Tasks_Priority = first.Tasks_Priority,
                        Tasks_Push = first.Tasks_Push,

                        RowType = hasParent && hasChild ? "ParentChildMerged" : first.RowType,
                        ShowChildMark = sg.Any(x => (int)x.BatchProductId != (int)x.RootId)
                    });
                }

                continue;
            }

    if (!items.Any(x => (int)x.BatchProductId == (int)x.RootId))
            {
                var stepGroups = items.GroupBy(x => x.StepOrder);

foreach (var sg in stepGroups)
{
    var first = sg.First();

    var totalQty = sg.Sum(x => (int)x.Qty);
    var totalMinutes = sg.Sum(x => (int)x.Qty * (int)x.EstimatedMinutes);

    result.Add(new EmployeeTaskRowV2
    {
        RootId = (int)first.RootId,
        BatchCode = first.BatchCode,
        BatchProductId = (int)first.BatchProductId,
        BatchPriority = first.BatchPriority,
        ProductToPartId = (int)first.ProductToPartId,
        ProductName = first.ProductName,
        TopPartName = first.TopPartName,
        StepName = first.StepName,

        DisplayQty = totalQty,
        DisplayMinutes = totalMinutes,

        Status = (int)first.Status,
        CanStart = first.CanStart,

        Assigned_To = first.Assigned_To,
        TopPartStepId = (int?)first.TopPartStepId ?? 0,
        Tasks_Priority = first.Tasks_Priority,
        Tasks_Push = first.Tasks_Push,

        RowType = "SingleChild",
        ShowChildMark = true
    });
}

continue;
            }
}

    return Ok(new
{
    InProgress = result.Where(x => x.Status == 2),
    Priority = result.Where(x => x.Status != 2 && x.BatchPriority),
    Normal = result.Where(x => x.Status != 2 && !x.BatchPriority)
});

}

[HttpPost("update-assignee-bulk")]
public async Task<IActionResult> UpdateAssigneeBulk([FromBody] List<UpdateAssigneeRequest> list)
{
    await _taskManagementService.UpdateAssigneeBulk(list);

    return Ok();
}


[HttpGet("unassigned-v2")]
public async Task<IActionResult> GetUnassignedTasksV2()
{
    return Ok(await _taskQueryService.GetUnassignedTasksV2());
}


[HttpGet("all-active")]
public async Task<IActionResult> GetAllActiveTasks()
{
    return Ok(await _taskService.GetAllActiveTasks());
}


// GET: /api/tasks/detail-tasks?batchProductId=123
[HttpGet("detail-tasks")]
public async Task<IActionResult> GetDetailTasks([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var result = await _detailService.GetDetailTasks(batchProductId);

    return Ok(result);
}

[HttpPost("start")]
public async Task<IActionResult> StartTask([FromBody] StartTaskRequest req)
{
    var result = await _taskService.StartByGroup(req.EmployeeId, req.DisplayGroupId);

    if (!result.Success)
        return BadRequest(result.Error);

    return Ok(result);
}

// GET: /api/tasks/ready-detail-parts?batchProductId=123
[HttpGet("ready-detail-parts")]
public async Task<IActionResult> GetReadyDetailParts([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var list = await _taskQueryService.GetReadyDetailParts(batchProductId);

    return Ok(list);
}

[HttpGet("assembly-summary")]
public async Task<IActionResult> GetAssemblySummary([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var result = await _assemblyTasksService.GetAssemblySummary(batchProductId);

    return Ok(result);
}

[HttpGet("inline-finishing-data")]
public async Task<IActionResult> GetInlineFinishingData(
    [FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var isInline = await _inlineFinishingTasksService
        .IsInlinePainting(batchProductId);

    if (!isInline)
    {
        return Ok(new
        {
            IsInline = false,
            DetailFinished = false,
            AvailableQty = 0
        });
    }

    var detailFinished = await _inlineFinishingTasksService
        .IsDetailFinished(batchProductId);

    var availableQty = await _inlineFinishingTasksService
        .GetAvailableInlineQty(batchProductId);

    return Ok(new
    {
        IsInline = true,
        DetailFinished = detailFinished,
        AvailableQty = availableQty
    });
}

[HttpGet("inline-assembly-data")]
public async Task<IActionResult> GetInlineAssemblyData(
    [FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest();

    var availableQty = await _inlineAssemblyTasksService
        .GetAvailableAssemblyQty(batchProductId);

    return Ok(new
    {
        AvailableQty = availableQty
    });
}

[HttpGet("inline-finishing-available")]
public async Task<ActionResult<int>> GetInlineFinishingAvailable(
    int batchProductId)
{
    var qty =
        await _inlineAssemblyTasksService
            .GetAvailableAssemblyQty(batchProductId);

    return Ok(qty);
}

[HttpPost("start-painting-session")]
public async Task<IActionResult> StartPaintingSession(
    [FromBody] StartPaintingSessionRequest dto)
{
    Console.WriteLine(
        $"PAINT SESSION -> emp={dto.EmployeeId} tasks={string.Join(",", dto.TaskIds)}"
    );

    var hasActivePaintingSession = await (
            from t in _db.Tasks
            join ts in _db.TopPartSteps
                on t.TopPartStep_ID equals ts.Id
            where t.Tasks_Status == 2
                && t.IsActive
                && ts.WorkCentrId == 4
            select t.ID
        ).AnyAsync();

            if (hasActivePaintingSession)
            {
                return Conflict("Krāsošanas sesija jau ir aktīva.");
            }

    var now = DateTime.UtcNow;

var tasks = await _db.Tasks
    .Where(t =>
        dto.TaskIds.Contains(t.ID) &&
        t.Tasks_Status == 1 &&
        t.IsActive)
    .ToListAsync();

foreach (var t in tasks)
{
    t.Tasks_Status = 2;
    t.Claimed_By = dto.EmployeeId;
    t.Started_At = now;
}

await _db.SaveChangesAsync();

    return Ok(new { updated = tasks.Count });
}

[HttpPost("finish-painting-session")]
public async Task<IActionResult> FinishPaintingSession(
    [FromBody] StartPaintingSessionRequest dto)
{
    var now = DateTime.UtcNow;

    var tasks = await _db.Tasks
        .Where(t =>
            dto.TaskIds.Contains(t.ID) &&
            t.Tasks_Status == 2 &&
            t.IsActive)
        .ToListAsync();

    foreach (var t in tasks)
    {
        t.Tasks_Status = 3;
        t.Finished_At = now;
    }

    await _db.SaveChangesAsync();

    return Ok(new { updated = tasks.Count });
}

    }
}
