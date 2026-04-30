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
using ManiApi.Services.Tasks;


namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TaskService _taskService;
    private readonly DetailTasksService _detailService;
    private readonly TaskManagementService _taskManagementService;
    private readonly FinishingFlowService _finishingFlowService;
    private readonly FinishingTasksService _finishingTasksService;
    private readonly TaskQueryService _taskQueryService;
    
    public TasksController(
        AppDbContext db,
        TaskService taskService,
        DetailTasksService detailService,
        TaskManagementService taskManagementService,
        FinishingFlowService finishingFlowService,
        FinishingTasksService finishingTasksService,
        TaskQueryService taskQueryService)
    {
        _db = db;
        _taskService = taskService;
        _detailService = detailService;
        _taskManagementService = taskManagementService;
        _taskQueryService = taskQueryService;
        _finishingFlowService = finishingFlowService;
        _finishingTasksService = finishingTasksService;
        _finishingFlowService = finishingFlowService;
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

    if (!result.Success)
        return Conflict(result.Error);

    return Ok(new { finished = true });

 /*   if (dto is null || dto.TaskId <= 0)
        return BadRequest("TaskId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    int currentStatus;

    // 1) Nolasām statusu un nolockojam rindu
    await using (var cmd = conn.CreateCommand())
    {
        cmd.Transaction = tx;
        cmd.CommandText = @"
SELECT Tasks_Status
FROM tasks
WHERE ID = @id AND IsActive = 1
FOR UPDATE;";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = dto.TaskId;
        cmd.Parameters.Add(p);

        var obj = await cmd.ExecuteScalarAsync();
        if (obj == null || obj == DBNull.Value)
        {
            await tx.RollbackAsync();
            return NotFound("Uzdevums nav atrasts vai ir neaktīvs.");
        }

        currentStatus = Convert.ToInt32(obj);
    }

    // 2) Atļaujam pabeigt tikai, ja ir 'Procesā' (2)
    if (currentStatus != 2)
    {
        await tx.RollbackAsync();
        return BadRequest("Pabeigt drīkst tikai uzdevumu ar statusu 'Procesā'.");
    }

    // 3) Nolasām Step_Type, Qty_Per_product, PlannedQty, CurrentDone, BatchProductId, VersionId
    int stepType;
    int qtyPerProduct;
    int plannedQty;
    int currentDone;
    int batchProductId;
    int versionId;
    int? ralColorId;

    await using (var info = conn.CreateCommand())
    {
        info.Transaction = tx;
        info.CommandText = @"
SELECT 
    ts.Step_Type,
    ptp.Qty_Per_product,
    COALESCE(SUM(bp.Planned_Qty), 0) AS PlannedQty,
    COALESCE(t.Qty_Done, 0)          AS CurrentDone,
    t.BatchProduct_ID,
    bp.Version_Id,
    COALESCE(t.Qty_Scrap, 0)         AS FinishingPlannedQty,
    t.RAL_Color_ID
FROM tasks t
JOIN toppartsteps ts     ON ts.ID = t.TopPartStep_ID
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
LEFT JOIN batches_products bp ON bp.ID = t.BatchProduct_ID AND bp.IsActive = 1
LEFT JOIN batches b ON b.ID = bp.Batch_Id AND b.IsActive = 1 AND b.Batches_Statuss = 1
WHERE t.ID = @id AND t.IsActive = 1
GROUP BY 
    ts.Step_Type,
    ptp.Qty_Per_product,
    t.Qty_Done,
    t.BatchProduct_ID,
    bp.Version_Id,
    t.Qty_Scrap;";

        var p = info.CreateParameter();
        p.ParameterName = "@id";
        p.Value = dto.TaskId;
        info.Parameters.Add(p);

        await using var rr = await info.ExecuteReaderAsync();
        if (!await rr.ReadAsync())
        {
            await tx.RollbackAsync();
            return NotFound("Uzdevuma dati nav atrasti.");
        }

        stepType       = rr.GetInt32(0);
        qtyPerProduct  = rr.GetInt32(1);
        plannedQty     = rr.GetInt32(2);
        currentDone    = rr.GetInt32(3);
        batchProductId = rr.GetInt32(4);
        versionId      = rr.IsDBNull(5) ? 0 : rr.GetInt32(5);
        var finishingPlannedQty = rr.IsDBNull(6) ? 0 : rr.GetInt32(6);
        ralColorId = rr.IsDBNull(7) ? null : rr.GetInt32(7);

        // Ja Finishing solis – pārrakstām plannedQty ar to, ko iedeva Finishing popup
        if (stepType == 3 && finishingPlannedQty > 0)
        {
            plannedQty = finishingPlannedQty;
        }
    }

    int newStatus  = 2;
    int newDoneOut = currentDone;

    // 4) Detailed / Assembly – pabeidzam VISU uzreiz
    if (stepType == 1)

    {
        var qtyDone = plannedQty * qtyPerProduct;
        newDoneOut = qtyDone;
        await using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = @"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Tasks_Status = 3,
    t.Finished_At  = CURRENT_TIMESTAMP,
    t.Qty_Done     = @qtyDone
WHERE t.IsActive = 1
  AND t.Tasks_Status = 2
  AND ts.ProductToPart_ID = (
      SELECT ts2.ProductToPart_ID
      FROM tasks t2
      JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
      WHERE t2.ID = @id
  )
  AND t.BatchProduct_ID IN (
      SELECT bp.ID
      FROM batches_products bp
      WHERE bp.IsActive = 1
        AND bp.Batch_Id = (
            SELECT bp0.Batch_Id FROM batches_products bp0
            WHERE bp0.ID = (
                SELECT BatchProduct_ID FROM tasks WHERE ID = @id
            )
        )
        AND bp.Version_Id = (
            SELECT bp0.Version_Id FROM batches_products bp0
            WHERE bp0.ID = (
                SELECT BatchProduct_ID FROM tasks WHERE ID = @id
            )
        )
  );";
            var p1 = upd.CreateParameter(); p1.ParameterName = "@qtyDone"; p1.Value = qtyDone;    upd.Parameters.Add(p1);
            var p2 = upd.CreateParameter(); p2.ParameterName = "@id";      p2.Value = dto.TaskId; upd.Parameters.Add(p2);
            await upd.ExecuteNonQueryAsync();
        }

        newStatus  = 3;
        newDoneOut = qtyDone;

        // 4.1) Detailed īpašais gadījums – kad VISI Detailed soļi pabeigti -> PLANNED -> DETAILED + atvērt Assembly
        if (stepType == 1 && batchProductId > 0)
        {
            int notFinishedDetailed = 0;
            await using (var cmdCheck = conn.CreateCommand())
            {
                cmdCheck.Transaction = tx;
                cmdCheck.CommandText = @"
SELECT COUNT(*)
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.BatchProduct_ID IN (
    SELECT bp.ID
    FROM batches_products bp
    WHERE bp.IsActive = 1
      AND bp.Batch_Id = (
          SELECT bp0.Batch_Id FROM batches_products bp0
          WHERE bp0.ID = @bpId
      )
      AND bp.Version_Id = (
          SELECT bp0.Version_Id FROM batches_products bp0
          WHERE bp0.ID = @bpId
      )
)
  AND t.IsActive = 1
  AND ts.Step_Type = 1
  AND t.Tasks_Status <> 3;";

                var pBp = cmdCheck.CreateParameter();
                pBp.ParameterName = "@bpId";
                pBp.Value = batchProductId;
                cmdCheck.Parameters.Add(pBp);

                var objCnt = await cmdCheck.ExecuteScalarAsync();
                notFinishedDetailed = (objCnt == null || objCnt == DBNull.Value)
                    ? 0
                    : Convert.ToInt32(objCnt);
            }

            if (notFinishedDetailed == 0)
            {
                // atveram Assembly (5 -> 1)
                await using (var cmdOpenAsm = conn.CreateCommand())
                {
                    cmdOpenAsm.Transaction = tx;
                    cmdOpenAsm.CommandText = @"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Tasks_Status = 1
WHERE t.BatchProduct_ID = @bpId
  AND t.IsActive = 1
  AND ts.Step_Type = 2
  AND t.Tasks_Status = 5;";

                    var pBp2 = cmdOpenAsm.CreateParameter();
                    pBp2.ParameterName = "@bpId";
                    pBp2.Value = batchProductId;
                    cmdOpenAsm.Parameters.Add(pBp2);

                    await cmdOpenAsm.ExecuteNonQueryAsync();
                }

                // PLANNED -> DETAILED (vienreiz per BatchProduct)
                if (versionId > 0)
                {
                    int existingDetailed = 0;
                    await using (var cmdCheckMove = conn.CreateCommand())
                    {
                        cmdCheckMove.Transaction = tx;
                        cmdCheckMove.CommandText = @"
SELECT COUNT(*)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'DETAILED'
  AND IsActive = 1;";

                        var pM = cmdCheckMove.CreateParameter();
                        pM.ParameterName = "@bpId";
                        pM.Value = batchProductId;
                        cmdCheckMove.Parameters.Add(pM);

                        var objM = await cmdCheckMove.ExecuteScalarAsync();
                        existingDetailed = (objM == null || objM == DBNull.Value)
                            ? 0
                            : Convert.ToInt32(objM);
                    }

                    if (existingDetailed == 0)
                    {
                        var totalQty = plannedQty * qtyPerProduct;

                        // PLANNED -
                        await using (var m1 = conn.CreateCommand())
                        {
                            m1.Transaction = tx;
                            m1.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, IsActive)
VALUES
    (@ver, @bpId, 'PLANNED', -@qty, CURRENT_TIMESTAMP, 1);";

                            m1.Parameters.Add(new MySqlParameter("@ver",  versionId));
                            m1.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
                            m1.Parameters.Add(new MySqlParameter("@qty",  totalQty));

                            await m1.ExecuteNonQueryAsync();
                        }

                        // DETAILED +
                        await using (var m2 = conn.CreateCommand())
                        {
                            m2.Transaction = tx;
                            m2.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'DETAILED', @qty, CURRENT_TIMESTAMP, @taskId, 1);";

                            m2.Parameters.Add(new MySqlParameter("@ver",    versionId));
                            m2.Parameters.Add(new MySqlParameter("@bpId",   batchProductId));
                            m2.Parameters.Add(new MySqlParameter("@qty",    totalQty));
                            m2.Parameters.Add(new MySqlParameter("@taskId", dto.TaskId));

                            await m2.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

    }
    
    // 4.2) Assembly īpašais gadījums – kad VISI Assembly soļi pabeigti -> DETAILED -> ASSEMBLY
    else if (stepType == 2 && batchProductId > 0 && versionId > 0)
        {
            int notFinishedAssembly = 0;
            await using (var cmdCheckAsm = conn.CreateCommand())
            {
                cmdCheckAsm.Transaction = tx;
                cmdCheckAsm.CommandText = @"
SELECT COUNT(*)
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.BatchProduct_ID = @bpId
  AND t.IsActive = 1
  AND ts.Step_Type = 2
  AND t.Tasks_Status <> 3;";

                var pBp = cmdCheckAsm.CreateParameter();
                pBp.ParameterName = "@bpId";
                pBp.Value = batchProductId;
                cmdCheckAsm.Parameters.Add(pBp);

                var objCnt = await cmdCheckAsm.ExecuteScalarAsync();
                notFinishedAssembly = (objCnt == null || objCnt == DBNull.Value)
                    ? 0
                    : Convert.ToInt32(objCnt);
            }

            if (notFinishedAssembly == 0)
            {
                int existingAsm = 0;
                await using (var cmdCheckMove = conn.CreateCommand())
                {
                    cmdCheckMove.Transaction = tx;
                    cmdCheckMove.CommandText = @"
SELECT COUNT(*)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'ASSEMBLY'
  AND IsActive = 1;";

                    var pM = cmdCheckMove.CreateParameter();
                    pM.ParameterName = "@bpId";
                    pM.Value = batchProductId;
                    cmdCheckMove.Parameters.Add(pM);

                    var objM = await cmdCheckMove.ExecuteScalarAsync();
                    existingAsm = (objM == null || objM == DBNull.Value)
                        ? 0
                        : Convert.ToInt32(objM);
                }

                if (existingAsm == 0)
                {
                    var totalQty = plannedQty * qtyPerProduct;

                    await using (var cmdMove = conn.CreateCommand())
                    {
                        cmdMove.Transaction = tx;
                        cmdMove.CommandText = @"
INSERT INTO stock_movements 
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'DETAILED', -@qty, CURRENT_TIMESTAMP, @taskId, 1),
    (@ver, @bpId, 'ASSEMBLY',  @qty, CURRENT_TIMESTAMP, @taskId, 1);";

                        var pVer = cmdMove.CreateParameter();
                        pVer.ParameterName = "@ver";
                        pVer.Value = versionId;
                        cmdMove.Parameters.Add(pVer);

                        var pBp3 = cmdMove.CreateParameter();
                        pBp3.ParameterName = "@bpId";
                        pBp3.Value = batchProductId;
                        cmdMove.Parameters.Add(pBp3);

                        var pQty = cmdMove.CreateParameter();
                        pQty.ParameterName = "@qty";
                        pQty.Value = totalQty;
                        cmdMove.Parameters.Add(pQty);

                        var pTask = cmdMove.CreateParameter();
                        pTask.ParameterName = "@taskId";
                        pTask.Value = dto.TaskId;
                        cmdMove.Parameters.Add(pTask);

                        await cmdMove.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    
    else
    {
        // 5) Finishing — apjoms jau ir Qty_Done (no popup), šeit tikai statusu pabeidzam + kustību uz STOCK.

        // 5.0) Task -> Finished
        await using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = @"
UPDATE tasks
   SET Tasks_Status = 3,
       Finished_At  = CURRENT_TIMESTAMP
 WHERE ID = @id;";
            upd.Parameters.Add(new MySqlParameter("@id", dto.TaskId));
            await upd.ExecuteNonQueryAsync();
        }

// 5.1) FINISHING -> STOCK kustība (idempotenta)
var qtyMove = currentDone;
if (qtyMove > 0 && batchProductId > 0 && versionId > 0)
{
    // ja STOCK jau ir ielikts šim taskam -> neko nedaram (idempotence)
    int alreadyDone = 0;
    await using (var chk = conn.CreateCommand())
    {
        chk.Transaction = tx;
        chk.CommandText = @"
SELECT COUNT(*)
FROM stock_movements
WHERE IsActive = 1
  AND Task_ID = @taskId
  AND BatchProduct_ID = @bpId
  AND Version_ID = @ver
  AND Move_Type = 'STOCK';";
        chk.Parameters.Add(new MySqlParameter("@taskId", dto.TaskId));
        chk.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
        chk.Parameters.Add(new MySqlParameter("@ver", versionId));

        alreadyDone = Convert.ToInt32(await chk.ExecuteScalarAsync());
    }

    if (alreadyDone == 0)
    {
        await using (var mv = conn.CreateCommand())
        {
            mv.Transaction = tx;
            mv.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, RAL_Color_ID, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'FINISHING', @ral, -@qty, CURRENT_TIMESTAMP, @taskId, 1),
    (@ver, @bpId, 'STOCK',     @ral,  @qty, CURRENT_TIMESTAMP, @taskId, 1);";

            mv.Parameters.Add(new MySqlParameter("@ver", versionId));
            mv.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
            mv.Parameters.Add(new MySqlParameter("@qty", qtyMove));
            mv.Parameters.Add(new MySqlParameter("@taskId", dto.TaskId));
            mv.Parameters.Add(new MySqlParameter("@ral", (object?)ralColorId ?? DBNull.Value));
            await mv.ExecuteNonQueryAsync();
        }
    }
}

        newStatus  = 3;
        newDoneOut = currentDone;
    }

// aizveram aktīvo darba sesiju
await using (var closeSession = conn.CreateCommand())
{
    closeSession.Transaction = tx;
    closeSession.CommandText = @"
UPDATE tasks_work_sessions
SET 
    EndTime = CURRENT_TIMESTAMP,
    DurationMinutes = TIMESTAMPDIFF(MINUTE, StartTime, CURRENT_TIMESTAMP)
WHERE Task_ID = @taskId
  AND EndTime IS NULL;";

    closeSession.Parameters.Add(new MySqlParameter("@taskId", dto.TaskId));

    await closeSession.ExecuteNonQueryAsync();
}

    await tx.CommitAsync();
    return Ok(new { taskId = dto.TaskId, status = newStatus, done = newDoneOut });*/
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
if (empId < 0)
    return BadRequest("empId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    string? employeeName = null;
    string? workCenterName = null;
    int? employeeWorkCenterId = null;


await using (var cmdHeader = conn.CreateCommand())
{
    cmdHeader.CommandText = @"
SELECT ID, Employee_Name, WorkCentrTypeID
FROM employees
WHERE ID = @empId;
";

    cmdHeader.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));

    await using var rHeader = await cmdHeader.ExecuteReaderAsync();
    if (await rHeader.ReadAsync())
        {
            employeeName = rHeader.IsDBNull(1) ? null : rHeader.GetString(1);
            employeeWorkCenterId = rHeader.IsDBNull(2) ? (int?)null : rHeader.GetInt32(2);
        }

    await rHeader.DisposeAsync();

    if (employeeWorkCenterId.HasValue)
        {
            await using var wcCmd = conn.CreateCommand();
            wcCmd.CommandText = @"SELECT Workcentr_Name FROM workcentr_type WHERE ID = @id";
            wcCmd.Parameters.Add(new MySqlParameter("@id", employeeWorkCenterId.Value));

            var wcObj = await wcCmd.ExecuteScalarAsync();
            workCenterName = wcObj?.ToString();
        }
}

    cmd.CommandText = @"
SELECT
    wc.Workcentr_Name AS WorkCenter,
    ts.WorkCentr_ID AS WorkCentrTypeID,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
    CASE 
    WHEN ts.Step_Type IN (1,2) THEN 
    CASE 
        WHEN bp.ProductToPart_ID IS NOT NULL 
             AND bp.ParentBatchProduct_ID IS NULL
        THEN bp.Planned_Qty   --  SINGLE CHILD
        ELSE bp.Planned_Qty * ptp.Qty_Per_product
    END
    WHEN ts.Step_Type = 3 THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS Qty,
    t.Tasks_Status AS Status,
    CASE
    WHEN t.Tasks_Status = 1 AND NOT EXISTS (
        SELECT 1
        FROM tasks t2
        JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
        WHERE t2.BatchProduct_ID = t.BatchProduct_ID
          AND ts2.ProductToPart_ID = ts.ProductToPart_ID
          AND ts2.Step_Order < ts.Step_Order
          AND t2.Tasks_Status <> 3
          AND t2.IsActive = 1
    )
    THEN 1
    WHEN t.Tasks_Status = 1
    THEN 0
    ELSE NULL
END AS CanStart,
    ts.Step_Order,
    ts.Step_Type,
    ts.Step_Name,
    ts.Estimated_Minutes,
    ts.ProductToPart_ID,
    tp.TopPart_Name,
    ts.IsFinal,
    t.Assigned_To,
    t.Tasks_Priority,
    t.Tasks_Push,
    t.Claimed_By,
    (
    CASE 
        WHEN EXISTS (
            SELECT 1
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
            AND bp2.Version_Id = bp.Version_Id
            AND bp2.ProductToPart_ID IS NULL
            AND bp2.IsActive = 1
        )
        THEN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
            AND bp2.Version_Id = bp.Version_Id
            AND bp2.ProductToPart_ID IS NULL
            AND bp2.IsActive = 1
            LIMIT 1
        )
        ELSE bp.ID
    END
) AS RootId,
ts.ID AS TopPartStepId,
CASE 
    WHEN bp.ProductToPart_ID IS NOT NULL 
         AND bp.ParentBatchProduct_ID IS NULL THEN 'SingleChild'
    ELSE 'Parent'
END AS RowType

FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
LEFT JOIN workcentr_type wc ON wc.ID = ts.WorkCentr_ID AND wc.IsActive = 1
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
LEFT JOIN producttopparts ptpParent
    ON ptpParent.Version_ID = bp.Version_Id
    AND ptpParent.TopPart_ID = ptp.TopPart_ID
    AND ptpParent.IsActive = 1
WHERE t.IsActive = 1
  AND t.Tasks_Status = 2
  AND t.Claimed_By = @empId
ORDER BY
  bp.is_priority DESC,
  bp.Priority ASC,
  t.Tasks_Priority DESC,
  ts.Step_Order ASC;
";

    cmd.Parameters.Add(new MySqlConnector.MySqlParameter("@wc", (object?)employeeWorkCenterId ?? DBNull.Value));

    cmd.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));

    var list = new List<object>();

    await using (var r = await cmd.ExecuteReaderAsync())
{
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            BatchPriority = false,
            WorkCenter = r.IsDBNull(0) ? null : r.GetString(0),
            workCenterTypeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1),
            WorkCenterSort = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            TaskId = r.GetInt32(3),
            BatchProductId = r.GetInt32(4),
            BatchCode = r.GetString(5),
            ProductName = r.GetString(6),
            Qty = r.IsDBNull(7) ? 0 : r.GetInt32(7),
            Status = r.GetInt32(8),
            CanStart = r.IsDBNull(9) ? (bool?)null : r.GetInt32(9) == 1,
            StepOrder = r.IsDBNull(10) ? 0 : r.GetInt32(10),
            StepType = r.GetInt32(11),
            StepName = r.IsDBNull(12) ? null : r.GetString(12),
            EstimatedMinutes = r.IsDBNull(13) ? 0 : r.GetInt32(13),
            ProductToPartId = r.GetInt32(14),
            TopPartName = r.IsDBNull(15) ? null : r.GetString(15),
            IsFinal = !r.IsDBNull(16) && r.GetBoolean(16),
            Assigned_To = r.IsDBNull(17) ? (int?)null : r.GetInt32(17),
            Tasks_Priority = !r.IsDBNull(18) && r.GetBoolean(18),
            Tasks_Push = !r.IsDBNull(19) && r.GetBoolean(19),
            Claimed_By = r.IsDBNull(20) ? (int?)null : r.GetInt32(20),
            RootId = r.GetInt32(21),
            TopPartStepId = r.GetInt32(22),
            RowType = r.IsDBNull(23) ? null : r.GetString(23),

        });
    }
}

// PRIORITĀRIE (status = 1, batch priority = true)

await using var cmd2 = conn.CreateCommand();
cmd2.CommandText = @"
SELECT
    wc.Workcentr_Name AS WorkCenter,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
CASE 
    WHEN ts.Step_Type IN (1,2) THEN 
        CASE 
            WHEN bp.ProductToPart_ID IS NOT NULL 
                 AND bp.ParentBatchProduct_ID IS NULL
            THEN bp.Planned_Qty
            ELSE bp.Planned_Qty * ptp.Qty_Per_product
        END
    WHEN ts.Step_Type = 3 THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS Qty,
    t.Tasks_Status AS Status,
    CASE
    WHEN t.Tasks_Status = 1 AND NOT EXISTS (
        SELECT 1
        FROM tasks t2
        JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
        WHERE t2.BatchProduct_ID = t.BatchProduct_ID
          AND ts2.ProductToPart_ID = ts.ProductToPart_ID
          AND ts2.Step_Order < ts.Step_Order
          AND t2.Tasks_Status <> 3
          AND t2.IsActive = 1
    )
    THEN 1
    WHEN t.Tasks_Status = 1
    THEN 0
    ELSE NULL
END AS CanStart,
    ts.Step_Order,
    ts.Step_Type,
    ts.Step_Name,
    ts.Estimated_Minutes,
    ts.ProductToPart_ID,
    tp.TopPart_Name,
    ts.IsFinal,
    t.Assigned_To,
    t.Tasks_Priority,
    t.Tasks_Push,
    t.Claimed_By, 
    (
    CASE 
        WHEN EXISTS (
            SELECT 1
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
            AND bp2.Version_Id = bp.Version_Id
            AND bp2.ProductToPart_ID IS NULL
            AND bp2.IsActive = 1
        )
        THEN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
            AND bp2.Version_Id = bp.Version_Id
            AND bp2.ProductToPart_ID IS NULL
            AND bp2.IsActive = 1
            LIMIT 1
        )
        ELSE bp.ID
    END
) AS RootId,
ts.ID AS TopPartStepId,
CASE 
    WHEN bp.ProductToPart_ID IS NOT NULL 
         AND bp.ParentBatchProduct_ID IS NULL THEN 'SingleChild'
    ELSE 'Parent'
END AS RowType

FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
LEFT JOIN workcentr_type wc ON wc.ID = ts.WorkCentr_ID AND wc.IsActive = 1
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
LEFT JOIN producttopparts ptpParent
    ON ptpParent.Version_ID = bp.Version_Id
    AND ptpParent.TopPart_ID = ptp.TopPart_ID
    AND ptpParent.IsActive = 1
WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  AND bp.is_priority = 1
AND 
(
    t.Assigned_To = @empId

    OR (
        t.Assigned_To IS NULL
        AND ts.WorkCentr_ID = @wc
    )
)
ORDER BY
  CASE WHEN t.Tasks_Push = 1 THEN 0 ELSE 1 END,
  t.Tasks_Priority DESC,
  CASE WHEN bp.is_priority = 1 THEN bp.Priority END ASC,
  ts.Step_Order ASC;
";

cmd2.Parameters.Add(new MySqlConnector.MySqlParameter("@wc", (object?)employeeWorkCenterId ?? DBNull.Value));
cmd2.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));
var priorityList = new List<object>();

await using (var r2 = await cmd2.ExecuteReaderAsync())
{
    while (await r2.ReadAsync())
    {
        priorityList.Add(new
        {
            BatchPriority = true,
            WorkCenter = r2.IsDBNull(0) ? null : r2.GetString(0),
            WorkCenterSort = r2.IsDBNull(1) ? (int?)null : r2.GetInt32(1),
            TaskId = r2.GetInt32(2),
            BatchProductId = r2.GetInt32(3),
            BatchCode = r2.GetString(4),
            ProductName = r2.GetString(5),
            Qty = r2.IsDBNull(6) ? 0 : r2.GetInt32(6),
            Status = r2.GetInt32(7),
            CanStart = r2.IsDBNull(8) ? (bool?)null : r2.GetInt32(8) == 1,
            StepOrder = r2.IsDBNull(9) ? 0 : r2.GetInt32(9),
            StepType = r2.GetInt32(10),
            StepName = r2.IsDBNull(11) ? null : r2.GetString(11),
            EstimatedMinutes = r2.IsDBNull(12) ? 0 : r2.GetInt32(12),
            ProductToPartId = r2.GetInt32(13),
            TopPartName = r2.IsDBNull(14) ? null : r2.GetString(14),
            IsFinal = !r2.IsDBNull(15) && r2.GetBoolean(15),
            Assigned_To = r2.IsDBNull(16) ? (int?)null : r2.GetInt32(16),
            Tasks_Priority = !r2.IsDBNull(17) && r2.GetBoolean(17),
            Tasks_Push = !r2.IsDBNull(18) && r2.GetBoolean(18),
            Claimed_By = r2.IsDBNull(19) ? (int?)null : r2.GetInt32(19),
            RootId = r2.GetInt32(20),
            TopPartStepId = r2.GetInt32(21),
            RowType = r2.IsDBNull(22) ? null : r2.GetString(22)

        });
    }
}
// SECĪGIE (status = 1, batch priority = false)

await using var cmd3 = conn.CreateCommand();
cmd3.CommandText = @"
SELECT
    wc.Workcentr_Name AS WorkCenter,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
CASE 
    WHEN ts.Step_Type IN (1,2) THEN 
        CASE 
            WHEN bp.ProductToPart_ID IS NOT NULL 
                 AND bp.ParentBatchProduct_ID IS NULL
            THEN bp.Planned_Qty
            ELSE bp.Planned_Qty * ptp.Qty_Per_product
        END
    WHEN ts.Step_Type = 3 THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS Qty,

    t.Tasks_Status AS Status,
    CASE
    WHEN t.Tasks_Status = 1 AND NOT EXISTS (
        SELECT 1
        FROM tasks t2
        JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
        WHERE t2.BatchProduct_ID = t.BatchProduct_ID
          AND ts2.ProductToPart_ID = ts.ProductToPart_ID
          AND ts2.Step_Order < ts.Step_Order
          AND t2.Tasks_Status <> 3
          AND t2.IsActive = 1
    )
    THEN 1
    WHEN t.Tasks_Status = 1
    THEN 0
    ELSE NULL
END AS CanStart,
    ts.Step_Order,
    ts.Step_Type,
    ts.Step_Name,
    ts.Estimated_Minutes,
    ts.ProductToPart_ID,
    tp.TopPart_Name,
    ts.IsFinal,
    t.Assigned_To,
    t.Tasks_Priority,
    t.Tasks_Push,
    t.Claimed_By,
    (
    CASE 
        WHEN EXISTS (
            SELECT 1
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
            AND bp2.Version_Id = bp.Version_Id
            AND bp2.ProductToPart_ID IS NULL
            AND bp2.IsActive = 1
        )
        THEN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
            AND bp2.Version_Id = bp.Version_Id
            AND bp2.ProductToPart_ID IS NULL
            AND bp2.IsActive = 1
            LIMIT 1
        )
        ELSE bp.ID
    END
) AS RootId,
ts.ID AS TopPartStepId,
CASE 
    WHEN bp.ProductToPart_ID IS NOT NULL 
         AND bp.ParentBatchProduct_ID IS NULL THEN 'SingleChild'
    ELSE 'Parent'
END AS RowType

FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
LEFT JOIN workcentr_type wc ON wc.ID = ts.WorkCentr_ID AND wc.IsActive = 1
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
LEFT JOIN producttopparts ptpParent
    ON ptpParent.Version_ID = bp.Version_Id
    AND ptpParent.TopPart_ID = ptp.TopPart_ID
    AND ptpParent.IsActive = 1
WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  AND bp.is_priority = 0
AND 
(
    t.Assigned_To = @empId

    OR (
        t.Assigned_To IS NULL
        AND ts.WorkCentr_ID = @wc
    )
)
ORDER BY
  CASE WHEN t.Tasks_Push = 1 THEN 0 ELSE 1 END,
  t.Tasks_Priority DESC,
  CASE WHEN bp.is_priority = 1 THEN bp.Priority END ASC,
  CASE WHEN bp.is_priority = 0 THEN bp.NormalOrder END ASC,
  ts.Step_Order ASC;
";

cmd3.Parameters.Add(new MySqlConnector.MySqlParameter("@wc", (object?)employeeWorkCenterId ?? DBNull.Value));
cmd3.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));
var normalList = new List<object>();

await using (var r3 = await cmd3.ExecuteReaderAsync())
{
    while (await r3.ReadAsync())
    {
        normalList.Add(new
        {
            BatchPriority = false,
            WorkCenter = r3.IsDBNull(0) ? null : r3.GetString(0),
            WorkCenterSort = r3.IsDBNull(1) ? (int?)null : r3.GetInt32(1),
            TaskId = r3.GetInt32(2),
            BatchProductId = r3.GetInt32(3),
            BatchCode = r3.GetString(4),
            ProductName = r3.GetString(5),
            Qty = r3.IsDBNull(6) ? 0 : r3.GetInt32(6),
            Status = r3.GetInt32(7),
            CanStart = r3.IsDBNull(8) ? (bool?)null : r3.GetInt32(8) == 1,
            StepOrder = r3.IsDBNull(9) ? 0 : r3.GetInt32(9),
            StepType = r3.GetInt32(10),
            StepName = r3.IsDBNull(11) ? null : r3.GetString(11),
            EstimatedMinutes = r3.IsDBNull(12) ? 0 : r3.GetInt32(12),
            ProductToPartId = r3.GetInt32(13),
            TopPartName = r3.IsDBNull(14) ? null : r3.GetString(14),
            IsFinal = !r3.IsDBNull(15) && r3.GetBoolean(15),
            Assigned_To = r3.IsDBNull(16) ? (int?)null : r3.GetInt32(16),
            Tasks_Priority = !r3.IsDBNull(17) && r3.GetBoolean(17),
            Tasks_Push = !r3.IsDBNull(18) && r3.GetBoolean(18),
            Claimed_By = r3.IsDBNull(19) ? (int?)null : r3.GetInt32(19),
            RootId = r3.GetInt32(20),  
            TopPartStepId = r3.GetInt32(21),   
            RowType = r3.IsDBNull(22) ? null : r3.GetString(22)  
        });
    }
}

return Ok(new
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
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    wc.Workcentr_Name AS WorkCenter,
    (
    CASE 
        WHEN EXISTS (
            SELECT 1
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.ProductToPart_ID IS NULL
              AND bp2.IsActive = 1
        )
        THEN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.ProductToPart_ID IS NULL
              AND bp2.IsActive = 1
            LIMIT 1
        )
        ELSE bp.ID
    END
) AS RootId,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    ts.ProductToPart_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
    tp.TopPart_Name,
    ts.Step_Name,
    t.TopPartStep_ID AS TopPartStepId,

CASE 
    WHEN ts.Step_Type IN (1,2) THEN 
        CASE 
            WHEN bp.ProductToPart_ID IS NOT NULL 
                 AND bp.ParentBatchProduct_ID IS NULL
            THEN bp.Planned_Qty
            ELSE bp.Planned_Qty * ptp.Qty_Per_product
        END
    WHEN ts.Step_Type = 3 THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS Qty,

    t.Tasks_Status AS Status,

    CASE
        WHEN t.Tasks_Status = 1 AND NOT EXISTS (
            SELECT 1
            FROM tasks t2
            JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
            WHERE t2.BatchProduct_ID = t.BatchProduct_ID
              AND ts2.ProductToPart_ID = ts.ProductToPart_ID
              AND ts2.Step_Order < ts.Step_Order
              AND t2.Tasks_Status <> 3
              AND t2.IsActive = 1
        )
        THEN 1
        ELSE 0
    END AS CanStart,

    ts.Step_Order,
    ts.Step_Type,
    ts.Estimated_Minutes,

    t.Assigned_To,
    bp.is_priority AS BatchPriority,
    COALESCE(t.Tasks_Priority, 0) AS Tasks_Priority,
    t.Tasks_Push

FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
LEFT JOIN workcentr_type wc ON wc.ID = ts.WorkCentr_ID AND wc.IsActive = 1
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID

WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  AND t.Assigned_To IS NULL
  
  ORDER BY
  CASE WHEN t.Tasks_Push = 1 THEN 0 ELSE 1 END,

  -- 1️⃣ Batch priority (priority batchi augšā)
  bp.is_priority DESC,

  -- 2️⃣ Ja priority → lieto Priority
  CASE 
      WHEN bp.is_priority = 1 THEN bp.Priority
  END ASC,

  -- 3️⃣ Ja ordinary → lieto NormalOrder
  CASE 
      WHEN bp.is_priority = 0 THEN bp.NormalOrder
  END ASC,

  -- 4️⃣ Task priority (iekš batch)
  t.Tasks_Priority DESC,

  -- 5️⃣ Step secība
  wc.ID,
  ts.Step_Order;
";

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        list.Add(new
        {
            WorkCenter = r.IsDBNull(0) ? null : r.GetString(0),
            RootId = r.GetInt32(1),
            WorkCenterSort = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            TaskId = r.GetInt32(3),
            BatchProductId = r.GetInt32(4),
            ProductToPartId = r.GetInt32(5),
            BatchCode = r.GetString(6),
            ProductName = r.GetString(7),
            TopPartName = r.GetString(8),
            StepName = r.GetString(9),
            TopPartStepId = r.GetInt32(10),
            Qty = r.IsDBNull(11) ? 0 : r.GetInt32(11),
            Status = r.GetInt32(12),
            CanStart = r.GetInt32(13) == 1,
            StepOrder = r.GetInt32(14),
            StepType = r.GetInt32(15),
            EstimatedMinutes = r.IsDBNull(16) ? 0 : r.GetInt32(16),
            Assigned_To = r.IsDBNull(17) ? (int?)null : r.GetInt32(17),
            BatchPriority = !r.IsDBNull(18) && r.GetBoolean(18),
            Tasks_Priority = !r.IsDBNull(19) && r.GetBoolean(19),
            Tasks_Push = !r.IsDBNull(20) && r.GetBoolean(20)
        });
    }

    var raw = list.Cast<dynamic>().ToList();

var groups = raw.GroupBy(x => new 
{ 
    RootId = (int)x.RootId,
    StepOrder = (int)x.StepOrder
});

var result = new List<object>();

foreach (var g in groups)
{
    var items = g.ToList();

var hasParent = items.Any(x => (int)x.BatchProductId == (int)x.RootId);
var hasChild = items.Any(x => (int)x.BatchProductId != (int)x.RootId);

    if (hasParent && hasChild)
    {
        var first = items.First();

        var parentQty = items
            .Where(x => (int)x.BatchProductId == (int)x.RootId)
            .Sum(x => (int)x.Qty);

        var childQty = items
            .Where(x => (int)x.BatchProductId != (int)x.RootId)
            .Sum(x => (int)x.Qty);

            var totalQty = parentQty + childQty;

            result.Add(new
                    {
                        first.WorkCenter,
                        first.WorkCenterSort,
                        first.TaskId,
                        first.BatchProductId,
                        first.ProductToPartId,
                        first.BatchCode,
                        first.ProductName,
                        first.TopPartName,
                        first.StepName,
                        first.TopPartStepId,
                        Qty = totalQty,
                        QtyBreakdown = $"{parentQty}+{childQty}",
                        EstimatedMinutes = first.EstimatedMinutes,
                        first.Status,
                        first.CanStart,
                        first.Assigned_To,
                        first.BatchPriority,
                        first.Tasks_Priority,
                        first.Tasks_Push,
                        first.StepOrder,
                        RowType = "ParentChildMerged"
                    });
                }
    else
{
    foreach (var t in items)
    {
        var isParent = hasParent
            ? (int)t.BatchProductId == (int)t.RootId
            : true;

        result.Add(new
        {
            t.WorkCenter,
            t.WorkCenterSort,
            t.TaskId,
            t.BatchProductId,
            t.ProductToPartId,
            t.BatchCode,
            t.ProductName,
            t.TopPartName,
            t.StepName,
            t.TopPartStepId,
            t.Qty,
            t.EstimatedMinutes,
            t.Status,
            t.CanStart,
            t.Assigned_To,
            t.BatchPriority,
            t.Tasks_Priority,
            t.Tasks_Push,
            t.StepOrder,

            RowType = isParent ? "Parent" : "ChildOnly"
        });
    }
}
}

return Ok(result);

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
    var val = await _taskQueryService.GetAssemblyAvailableUi(batchProductId);
    return Ok(val);
    
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
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"SELECT
    wc.Workcentr_Name AS WorkCenter,
    CASE 
    WHEN EXISTS (
        SELECT 1
        FROM batches_products bp2
        WHERE bp2.Batch_Id = bp.Batch_Id
          AND bp2.Version_Id = bp.Version_Id
          AND bp2.ProductToPart_ID IS NULL
          AND bp2.IsActive = 1
    )
    THEN (
        SELECT bp2.ID
        FROM batches_products bp2
        WHERE bp2.Batch_Id = bp.Batch_Id
          AND bp2.Version_Id = bp.Version_Id
          AND bp2.ProductToPart_ID IS NULL
          AND bp2.IsActive = 1
        LIMIT 1
    )
    ELSE bp.ID
END AS RootId,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    ts.ProductToPart_ID,
    b.Batches_Code,
    p.Product_Name,
    tp.TopPart_Name,
    ts.Step_Name,
    t.TopPartStep_ID,
    CASE 
        WHEN ts.Step_Type IN (1,2) THEN 
            bp.Planned_Qty
        WHEN ts.Step_Type = 3 THEN t.Qty_Done
        ELSE bp.Planned_Qty
    END AS Qty,
    CASE 
WHEN ts.Step_Type IN (1,2) THEN 
    bp.Planned_Qty * ts.Estimated_Minutes
WHEN ts.Step_Type = 3 THEN 
    t.Qty_Done * ts.Estimated_Minutes
ELSE 
    bp.Planned_Qty * ts.Estimated_Minutes
END AS Estimated_Minutes,
    t.Tasks_Status,
    CASE
        WHEN t.Tasks_Status = 1 AND NOT EXISTS (
            SELECT 1
            FROM tasks t2
            JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
            WHERE t2.BatchProduct_ID = t.BatchProduct_ID
              AND ts2.ProductToPart_ID = ts.ProductToPart_ID
              AND ts2.Step_Order < ts.Step_Order
              AND t2.Tasks_Status <> 3
              AND t2.IsActive = 1
        )
        THEN 1 ELSE 0
    END AS CanStart,
    t.Assigned_To,
    bp.is_priority,
    COALESCE(t.Tasks_Priority, 0),
    t.Tasks_Push,
    ts.Step_Order,
    bp.ParentBatchProduct_ID
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
LEFT JOIN workcentr_type wc ON wc.ID = ts.WorkCentr_ID AND wc.IsActive = 1
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  AND t.Assigned_To IS NULL;
";

    var raw = new List<dynamic>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        raw.Add(new
        {
            WorkCenter = r.IsDBNull(0) ? null : r.GetString(0),
            RootId = r.IsDBNull(1) ? 0 : r.GetInt32(1),
            WorkCenterSort = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            TaskId = r.GetInt32(3),
            BatchProductId = r.GetInt32(4),
            ProductToPartId = r.GetInt32(5),
            BatchCode = r.IsDBNull(6) ? null : r.GetString(6),
            ProductName = r.IsDBNull(7) ? null : r.GetString(7),
            TopPartName = r.IsDBNull(8) ? null : r.GetString(8),
            StepName = r.IsDBNull(9) ? null : r.GetString(9),
            TopPartStepId = r.IsDBNull(10) ? 0 : r.GetInt32(10),
            Qty = r.IsDBNull(11) ? 0 : r.GetInt32(11),
            EstimatedMinutes = r.IsDBNull(12) ? 0 : r.GetInt32(12),
            Status = r.IsDBNull(13) ? 0 : r.GetInt32(13),
            CanStart = !r.IsDBNull(14) && r.GetInt32(14) == 1,
            Assigned_To = r.IsDBNull(15) ? null : (int?)r.GetInt32(15),
            BatchPriority = !r.IsDBNull(16) && r.GetBoolean(16),
            Tasks_Priority = !r.IsDBNull(17) && r.GetBoolean(17),
            Tasks_Push = !r.IsDBNull(18) && r.GetBoolean(18),
            StepOrder = r.IsDBNull(19) ? 0 : r.GetInt32(19),
            ParentBatchProductId = r.IsDBNull(20) ? (int?)null : r.GetInt32(20),
        });
    }

    var result = new List<UnassignedTaskV2Dto>();

var groups = raw.GroupBy(x => new 
{ 
    x.WorkCenter,
    x.RootId,
    x.StepOrder
});

foreach (var g in groups)
{
    var items = g.ToList();

    var parents = items.Where(x => x.ProductToPartId == 0).ToList();
    var childs = items.Where(x => x.ProductToPartId != 0).ToList();

    var hasParent = parents.Any();
    var hasChild = childs.Any();

    // 🔹 Parent only
    if (hasParent && !hasChild)
    {
        foreach (var t in items)
        {
            result.Add(new UnassignedTaskV2Dto
            {
                WorkCenter = t.WorkCenter,
                WorkCenterSort = t.WorkCenterSort,
                TaskId = t.TaskId,
                RootId = t.RootId,
                BatchProductId = t.BatchProductId,
                ProductToPartId = t.ProductToPartId,
                BatchCode = t.BatchCode,
                ProductName = t.ProductName,
                TopPartStepId = t.TopPartStepId,
                TopPartName = t.TopPartName,
                StepName = t.StepName,

                Qty = t.Qty,
                EstimatedMinutes = t.EstimatedMinutes,

                Status = t.Status,
                CanStart = t.CanStart,

                Assigned_To = t.Assigned_To,
                BatchPriority = t.BatchPriority,
                Tasks_Priority = t.Tasks_Priority,
                Tasks_Push = t.Tasks_Push,

                StepOrder = t.StepOrder,
                RowType = "Parent"
            });
        }

        continue;
    }

    // 🔹 Parent + Child
    if (hasParent && hasChild)
    {
        var parentQty = parents.Sum(x => (int)x.Qty);
        var childQty = childs.Sum(x => (int)x.Qty);

        var first = items.First();
        var totalQty = parentQty + childQty;

        result.Add(new UnassignedTaskV2Dto
        {
            WorkCenter = first.WorkCenter,
            WorkCenterSort = first.WorkCenterSort,
            TaskId = first.TaskId,
            RootId = first.RootId,
            BatchProductId = first.BatchProductId,
            ProductToPartId = first.ProductToPartId,
            BatchCode = first.BatchCode,
            ProductName = first.ProductName,
            TopPartName = first.TopPartName,
            TopPartStepId = first.TopPartStepId,
            StepName = first.StepName,

            Qty = totalQty,
            QtyBreakdown = childQty > 0 ? $"{parentQty}+{childQty}" : parentQty.ToString(),
            EstimatedMinutes = first.EstimatedMinutes,

            Status = first.Status,
            CanStart = first.CanStart,

            Assigned_To = first.Assigned_To,
            BatchPriority = first.BatchPriority,
            Tasks_Priority = first.Tasks_Priority,
            Tasks_Push = first.Tasks_Push,

            StepOrder = first.StepOrder,
            RowType = "ParentChildMerged"
        });

        continue;
    }

    // 🔹 Single Child
    if (!hasParent && hasChild)
    {
        foreach (var t in items)
        {
            result.Add(new UnassignedTaskV2Dto
            {
                WorkCenter = t.WorkCenter,
                WorkCenterSort = t.WorkCenterSort,
                TaskId = t.TaskId,
                RootId = t.RootId,
                BatchProductId = t.BatchProductId,
                ProductToPartId = t.ProductToPartId,
                BatchCode = t.BatchCode,
                ProductName = t.ProductName,
                TopPartName = t.TopPartName,
                TopPartStepId = t.TopPartStepId,
                StepName = t.StepName,

                Qty = t.Qty,
                EstimatedMinutes = t.EstimatedMinutes,

                Status = t.Status,
                CanStart = t.CanStart,

                Assigned_To = t.Assigned_To,
                BatchPriority = t.BatchPriority,
                Tasks_Priority = t.Tasks_Priority,
                Tasks_Push = t.Tasks_Push,

                StepOrder = t.StepOrder,
                RowType = "SingleChild"
            });
        }
    }
}

return Ok(result);
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



    }
}
