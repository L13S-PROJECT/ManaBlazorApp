// Tasks/taskservice.cs -> TaskService klase ar GetForEmployee, ClaimTask, FinishTask metodēm

using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using System.Data;
using MySqlConnector;
using System.Data.Common;
using ManiApi.Services.Tasks;


namespace ManiApi.Services.Tasks
{
    public class TaskService
    {
        private readonly AppDbContext _db;
        private readonly TaskQueryService _queryService;
        public TaskService(AppDbContext db, TaskQueryService queryService)
        {
            _db = db;
            _queryService = queryService;
        }
       

public Task<List<TaskRowDto>> GetForEmployee(int empId)
    => _queryService.GetForEmployee(empId);
    
public Task<List<TaskRowDto>> GetAllActiveTasks()
    => _queryService.GetAllActiveTasks();

public async Task<(bool Success, string? Error)> ClaimTask(int taskId, int empId)
{
    // 🔹 Basic validācija
    if (taskId <= 0 || empId <= 0)
        return (false, "Bad request");

    // 🔹 Grupas līmeņa validācija (drīkst startēt tikai pareizo tasku grupā)
    var groupValidation = await ValidateGroupStart(taskId, empId);
    if (!groupValidation.Success)
        return (false, groupValidation.Error);

    Console.WriteLine($"CLAIM CALLED: taskId={taskId}, empId={empId}");

    // 🔹 DB connection + transaction sākums
    var conn = _db.Database.GetDbConnection();
    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var tx = await conn.BeginTransactionAsync();

    // 1️⃣ Pārbaude: vai darbiniekam jau nav aktīvs darbs (status = 2)
    var activeCheck = await _queryService.CheckEmployeeHasActiveTask(conn, tx, empId);
    if (!activeCheck.Success)
    {
        await tx.RollbackAsync();
        return (false, activeCheck.Error);
    }

    // 2️⃣ Pārbaude: prioritāte un step secība (nedrīkst apsteigt citus darbus)
    var priorityCheck = await _queryService.CheckPriorityAndOrder(conn, tx, taskId, empId);
    if (!priorityCheck.Success)
    {
        await tx.RollbackAsync();
        return (false, priorityCheck.Error);
    }

    var currentStepOrder = priorityCheck.StepOrder;

    // 3️⃣ Nosaka:
    // - rootId (kopējais batch root)
    // - hasRoot (vai ir parent/child struktūra)
    // - pārbauda vai iepriekšējie step ir pabeigti
    var stepValidation = await ValidateStepOrder(conn, tx, taskId, currentStepOrder);
    var stepType = stepValidation.StepType;

    if (!stepValidation.Success)
    {
        await tx.RollbackAsync();
        return (false, stepValidation.Error);
    }

    var rootId = stepValidation.RootId;
    var hasRoot = stepValidation.HasRoot;

    // 4️⃣ Nosaka scenāriju:
    // A_Parent  → parasts
    // B_Root    → parent + child kopā
    // C_Child   → tikai child
    var scenario =
        hasRoot ? TaskScenario.B_Root :
        stepType == 1 ? TaskScenario.C_Child :
        TaskScenario.A_Parent;

    // 5️⃣ Pārbaude: vai task vispār eksistē un ir aktīvs
    var taskExists = await _queryService.ValidateTaskExists(conn, tx, taskId);
    if (!taskExists.Success)
    {
        await tx.RollbackAsync();
        return (false, taskExists.Error);
    }

    // 6️⃣ Root gadījumā:
    // pārbauda vai VISI iepriekšējie step visiem child/parent ir pabeigti
    var rootValidation = await ValidateRootStep(
        conn, tx, taskId, rootId, currentStepOrder, scenario);

    if (!rootValidation.Success)
    {
        await tx.RollbackAsync();
        return (false, rootValidation.Error);
    }

    // 7️⃣ Status maiņa → uz "Procesā" (status = 2)
    var updateResult = await UpdateTaskToInProgress(
        conn, tx, taskId, empId, rootId, currentStepOrder, scenario);

    if (!updateResult.Success)
    {
        await tx.RollbackAsync();
        return (false, updateResult.Error);
    }

    // ℹ️ Claim NEVEIC stock kustības
    // (Finishing / Assembly loģika notiek FinishTask vai OpenFinishing)

    // 8️⃣ Izveido darba sesiju (tracking: kurš, kad sāka)
    await InsertWorkSession(conn, tx, taskId, empId, rootId, currentStepOrder, hasRoot);

    // 🔹 Commit
    await tx.CommitAsync();

    return (true, null);
}

public async Task<(bool Success, string? Error)> FinishTask(int taskId, int? qtyDoneAdd)
{
    
// 🔹 DB connection + transaction sākums
        var conn = _db.Database.GetDbConnection();

        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        await using var tx = await conn.BeginTransactionAsync();

taskId = await _queryService.ResolveRootTaskId(conn, tx, taskId);

Console.WriteLine($"FINISH -> resolved taskId = {taskId}");

// 1️⃣ Pārbaude: vai task eksistē un ir "Procesā"

var statusCheck = await _queryService.ValidateTaskIsInProgress(conn, tx, taskId);

Console.WriteLine($"FINISH STATUS -> success={statusCheck.Success} error={statusCheck.Error}");

if (!statusCheck.Success)
{
    await tx.RollbackAsync();
    return (false, statusCheck.Error);
}

// 2️⃣ Nolasām task datus (step, qty, batch, u.c.)

var taskInfo = await _queryService.GetTaskDetails(conn, tx, taskId);

int stepType = taskInfo.StepType;
int productToPartId = taskInfo.ProductToPartId;
int currentStepOrder = taskInfo.StepOrder;
int qtyPerProduct = taskInfo.QtyPerProduct;
int plannedQty = taskInfo.PlannedQty;
int currentDone = taskInfo.CurrentDone;
int batchProductId = taskInfo.BatchProductId;
int versionId = taskInfo.VersionId;
int? ralColorId = taskInfo.RalColorId;

// 3️⃣ Nosakām root struktūru (parent/child)

var rootInfo = await _queryService.GetRootInfo(conn, tx, taskId);

int rootId = rootInfo.RootId;
bool hasRoot = rootInfo.HasRoot;

// 4️⃣ Nosakām scenāriju (A / B / C)

var scenario =
    hasRoot ? TaskScenario.B_Root :
    stepType == 1 ? TaskScenario.C_Child :
    TaskScenario.A_Parent;

// 5️⃣ Izpilde pēc B/A/C scenārija

// Detailed (StepType = 1) → pabeidzam visu
if (scenario == TaskScenario.B_Root)
{
    await HandleRootScenario(
        conn, tx,
        stepType,
        plannedQty,
        qtyPerProduct,
        currentStepOrder,
        rootId);
    // auto-create painting tasks root child detaļām
await CreateRootPaintingTasksIfNeeded(
    conn,
    tx,
    rootId
);

}

else if (scenario == TaskScenario.A_Parent)
{   
    if (stepType == 1)
    {
        await HandleParentDetailStep(conn, tx, taskId, batchProductId, rootId);
    }
    else if (stepType == 2)
{
    await HandleParentAssemblyStep(
        conn, tx,
        taskId,
        rootId,
        plannedQty,
        qtyPerProduct,
        batchProductId,
        versionId,
        currentDone,
        ralColorId);
}
}


else if (scenario == TaskScenario.C_Child)
{
    await HandleChildScenario(
        conn, tx,
        taskId,
        rootId,
        batchProductId,
        versionId,
        plannedQty);
    // auto-create painting task child scenārijam
await CreateChildPaintingTasksIfNeeded(
    conn,
    tx,
    batchProductId
);
    
}
else
{
    await tx.RollbackAsync();
    return (false, "Neatbalstīts scenārijs.");
}

// 6️⃣ Aizveram darba sesiju

await CloseWorkSession(conn, tx, taskId);

// 🔹 Commit

await tx.CommitAsync();
return (true, null);
}

private enum TaskScenario
{
    A_Parent,      // Parasts (nav root)
    B_Root,        // Parent + Child kopā
    C_Child        // Tikai child
}

public async Task<(bool Success, string? Error)> StartByGroup(int empId, long displayGroupId)
{
    var tasks = await GetForEmployee(empId);

    var groupTasks = tasks
        .Where(t => t.DisplayGroupId == displayGroupId)
        .ToList();

    if (!groupTasks.Any())
        return (false, "Nav pieejamu uzdevumu šajā grupā.");

    var targetTask = groupTasks
        .Where(t => t.Status == 1 && t.CanStart)
        .OrderBy(t => t.Tasks_Push ? 0 : 1)
        .ThenByDescending(t => t.PriorityLevel)
        .ThenBy(t => t.StepOrder)
        .ThenBy(t => t.TaskId)
        .FirstOrDefault();

    if (targetTask == null)
        return (false, "Nav startējamu uzdevumu.");

    return await ClaimTask(targetTask.TaskId, empId);
}

private async Task<bool> CheckDetailFinished(DbConnection conn, DbTransaction tx, int rootId)
{
   return await _queryService.IsDetailPhaseFinishedAll(conn, tx, rootId);
}


private async Task<(bool Success, string? Error)> ValidateGroupStart(int taskId, int empId)
{
    var availableTasks = await GetForEmployee(empId);

    var clicked = availableTasks.FirstOrDefault(x => x.TaskId == taskId);

    if (clicked == null)
        return (false, "Task not found.");

    var groupTasks = availableTasks
        .Where(t => t.DisplayGroupId == clicked.DisplayGroupId)
        .ToList();

    var target = groupTasks.FirstOrDefault(t => t.TaskId == taskId);

    var firstAvailable = groupTasks
        .Where(t => t.Status == 1 && t.CanStart)
        .OrderBy(t => t.Tasks_Push ? 0 : 1)
        .ThenByDescending(t => t.PriorityLevel)
        .ThenBy(t => t.StepOrder)
        .ThenBy(t => t.TaskId)
        .FirstOrDefault();

    if (target == null || firstAvailable == null || target.TaskId != firstAvailable.TaskId)
        return (false, "Drīkst sākt tikai nākamo prioritāro darbu.");

    return (true, null);
}


private async Task<(bool Success, string? Error)> UpdateTaskToInProgress(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int empId,
    int rootId,
    int currentStepOrder,
    TaskScenario scenario)
{
    await using var upd = conn.CreateCommand();
    upd.Transaction = tx;

    if (scenario == TaskScenario.B_Root)
    {
        upd.CommandText = @"
        UPDATE tasks t
        JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        SET t.Tasks_Status = 2,
            t.Claimed_By   = @emp,
            t.Started_At   = CURRENT_TIMESTAMP
        WHERE t.IsActive = 1
        AND t.Tasks_Status = 1
        AND t.TopPartStep_ID = (
            SELECT TopPartStep_ID
            FROM tasks
            WHERE ID = @taskId
        )
        AND (
                bp.ID = @rootId
            OR bp.ParentBatchProduct_ID = @rootId
        )
        AND (
                (@scenario = 1)
                OR t.ID = @taskId
            )";
    }
    else
    {
        upd.CommandText = @"
UPDATE tasks 
SET Tasks_Status = 2, 
    Claimed_By   = @emp,
    Started_At   = CURRENT_TIMESTAMP
WHERE ID = @taskId
  AND Tasks_Status = 1 
  AND IsActive = 1;";
    }

    upd.Parameters.Add(new MySqlParameter("@emp", empId));
    upd.Parameters.Add(new MySqlParameter("@taskId", taskId));
    upd.Parameters.Add(new MySqlParameter("@rootId", rootId));
    upd.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));
    upd.Parameters.Add(new MySqlParameter("@scenario", scenario == TaskScenario.B_Root ? 1 : 0));

    var affected = await upd.ExecuteNonQueryAsync();

    if (affected == 0)
        return (false, "Darbs vairs nav pieejams.");

    return (true, null);
}

private async Task InsertWorkSession(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int empId,
    int rootId,
    int currentStepOrder,
    bool hasRoot)
{
    await using var session = conn.CreateCommand();
    session.Transaction = tx;

    session.CommandText = hasRoot ? @"
INSERT INTO tasks_work_sessions (Task_ID, Employee_ID, StartTime)
SELECT t2.ID, @emp, CURRENT_TIMESTAMP
FROM tasks t2
JOIN batches_products bp2 ON bp2.ID = t2.BatchProduct_ID
JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
WHERE t2.IsActive = 1
  AND t2.Tasks_Status = 2
  AND ts2.ID = (
    SELECT TopPartStep_ID
    FROM tasks
    WHERE ID = @taskId
)
AND t2.BatchProduct_ID IN (
    SELECT bp3.ID
    FROM batches_products bp3
    WHERE bp3.IsActive = 1
      AND (
            bp3.ID = @rootId
         OR bp3.ParentBatchProduct_ID = @rootId
      )
)
AND ts2.Step_Order = @curStepOrder
;"
:
@"
INSERT INTO tasks_work_sessions
    (Task_ID, Employee_ID, StartTime)
VALUES
    (@taskId, @emp, CURRENT_TIMESTAMP);";

    session.Parameters.Add(new MySqlParameter("@taskId", taskId));
    session.Parameters.Add(new MySqlParameter("@emp", empId));
    session.Parameters.Add(new MySqlParameter("@rootId", rootId));
    session.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));

    await session.ExecuteNonQueryAsync();
}

private async Task<(bool Success, string? Error)> ValidateRootStep(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int rootId,
    int currentStepOrder,
    TaskScenario scenario)
{
    if (scenario != TaskScenario.B_Root)
        return (true, null);

    var rootCheck = await _queryService.CheckRootStepOrder(conn, tx, taskId, rootId, currentStepOrder);

    if (!rootCheck.Success)
        return (false, rootCheck.Error);

    return (true, null);
}

private async Task<(bool Success, string? Error, int RootId, bool HasRoot, int StepType)> ValidateStepOrder(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int currentStepOrder)
{
    var stepCheck = await _queryService.CheckStepOrder(conn, tx, taskId, currentStepOrder);

    if (!stepCheck.Success)
        return (false, stepCheck.Error, 0, false, 0);
    

    return (true, null, stepCheck.RootId, stepCheck.HasRoot, stepCheck.StepType);
}

private async Task HandleRootScenario(
    DbConnection conn,
    DbTransaction tx,
    int stepType,
    int plannedQty,
    int qtyPerProduct,
    int currentStepOrder,
    int rootId)
{
    var qtyDone = stepType == 1
        ? plannedQty * qtyPerProduct
        : plannedQty;

Console.WriteLine($"ROOT SCENARIO UPDATE -> rootId={rootId} stepOrder={currentStepOrder}");

    await using (var upd = conn.CreateCommand())
    {
        upd.Transaction = tx;
        upd.CommandText = @"
        UPDATE tasks t
        JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        SET t.Tasks_Status = 3,
            t.Finished_At  = CURRENT_TIMESTAMP,
            t.Qty_Done     = @qtyDone
        WHERE t.IsActive = 1
        AND t.Tasks_Status = 2
        AND ts.Step_Order = @curStepOrder
        AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;";

        upd.Parameters.Add(new MySqlParameter("@qtyDone", qtyDone));
        upd.Parameters.Add(new MySqlParameter("@rootId", rootId));
        upd.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));

        await upd.ExecuteNonQueryAsync();

        Console.WriteLine($"ROOT UPDATE AFFECTED -> done");
    }

    bool detailFinished = await CheckDetailFinished(conn, tx, rootId);

    if (detailFinished)
    {
        await using (var openAssembly = conn.CreateCommand())
        {
            openAssembly.Transaction = tx;
            openAssembly.CommandText = @"
            UPDATE tasks t
            JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            SET t.Tasks_Status = 1
            WHERE t.IsActive = 1
            AND ts.Step_Type = 2
            AND t.Tasks_Status = 5
            AND bp.ProductToPart_ID IS NULL
            AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;";

            openAssembly.Parameters.Add(new MySqlParameter("@rootId", rootId));

            await openAssembly.ExecuteNonQueryAsync();
        }
    }
}

private async Task HandleParentDetailStep(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int batchProductId,
    int rootId)
{
    bool detailFinished = false;

    await using (var upd = conn.CreateCommand())
    {
        upd.Transaction = tx;
        upd.CommandText = @"
        UPDATE tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        SET t.Tasks_Status = 3,
            t.Finished_At  = CURRENT_TIMESTAMP
        WHERE t.ID = @id;";

        upd.Parameters.Add(new MySqlParameter("@id", taskId));

        await upd.ExecuteNonQueryAsync();
        detailFinished = await CheckDetailFinished(conn, tx, rootId);
    }

    if (detailFinished)
    {
        await using (var openAssembly = conn.CreateCommand())
        {
            openAssembly.Transaction = tx;
            openAssembly.CommandText = @"
            UPDATE tasks t
            JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            SET t.Tasks_Status = 1
            WHERE t.IsActive = 1
            AND ts.Step_Type = 2
            AND t.Tasks_Status = 5
            AND bp.ProductToPart_ID IS NULL
            AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;";

            openAssembly.Parameters.Add(new MySqlParameter("@rootId", rootId));

            await openAssembly.ExecuteNonQueryAsync();
        }
    }
}

private async Task HandleParentAssemblyStep(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int rootId,
    int plannedQty,
    int qtyPerProduct,
    int batchProductId,
    int versionId,
    int currentDone,
    int? ralColorId)
{
    bool detailFinished = await CheckDetailFinished(conn, tx, rootId);

    await using (var upd = conn.CreateCommand())
    {
        Console.WriteLine($"UPDATING TASK -> {taskId} TO STATUS=3");
        upd.Transaction = tx;
        upd.CommandText = @"
        UPDATE tasks
        SET Tasks_Status = 3,
            Finished_At  = CURRENT_TIMESTAMP
        WHERE ID = @id;";

        upd.Parameters.Add(new MySqlParameter("@id", taskId));
        await upd.ExecuteNonQueryAsync();
    }

    bool notFinishedAssembly = await _queryService.HasNotFinishedAssembly(conn, tx, rootId);

    if (!notFinishedAssembly)
    {
    bool existingAsm = await _queryService.HasAssemblyStockMovement(conn, tx, rootId);

        if (!existingAsm)
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

                cmdMove.Parameters.Add(new MySqlParameter("@ver", versionId));
                cmdMove.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
                cmdMove.Parameters.Add(new MySqlParameter("@qty", totalQty));
                cmdMove.Parameters.Add(new MySqlParameter("@taskId", taskId));

                await cmdMove.ExecuteNonQueryAsync();
            }
        }
    }

    bool isFinalStep = await _queryService.IsFinalStep(conn, tx, taskId);

    if (isFinalStep && detailFinished && !notFinishedAssembly)
    {
        var qtyMove = currentDone;

        if (qtyMove > 0 && batchProductId > 0 && versionId > 0)
        {
        bool alreadyDone = await _queryService.HasStockMovement(conn, tx, taskId, batchProductId, versionId);

            if (!alreadyDone)
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
                    mv.Parameters.Add(new MySqlParameter("@taskId", taskId));
                    mv.Parameters.Add(new MySqlParameter("@ral", (object?)ralColorId ?? DBNull.Value));

                    await mv.ExecuteNonQueryAsync();
                }
            }
        }
    }
}

private async Task HandleChildScenario(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int rootId,
    int batchProductId,
    int versionId,
    int plannedQty)
{
    // 1) Task -> Finished
    await using (var upd = conn.CreateCommand())
    {
        Console.WriteLine($"UPDATING TASK -> {taskId} TO STATUS=3");
        upd.Transaction = tx;
        upd.CommandText = @"
        UPDATE tasks
        SET Tasks_Status = 3,
            Finished_At  = CURRENT_TIMESTAMP
        WHERE ID = @id;";

        upd.Parameters.Add(new MySqlParameter("@id", taskId));
        await upd.ExecuteNonQueryAsync();
    }

    bool detailFinished = await CheckDetailFinished(conn, tx, rootId);

    bool isFinalStep = await _queryService.IsFinalStep(conn, tx, taskId);

    if (detailFinished && versionId > 0 && isFinalStep)
    {
        bool alreadyDone = await _queryService.HasDetailedMovement(conn, tx, batchProductId);

        if (!alreadyDone)
        {
            var totalQty = plannedQty;

            await using (var m2 = conn.CreateCommand())
            {
                m2.Transaction = tx;
                m2.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'DETAILED', @qty, CURRENT_TIMESTAMP, @taskId, 1);";

                m2.Parameters.Add(new MySqlParameter("@ver", versionId));
                m2.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
                m2.Parameters.Add(new MySqlParameter("@qty", totalQty));
                m2.Parameters.Add(new MySqlParameter("@taskId", taskId));

                await m2.ExecuteNonQueryAsync();
            }
        }

        await using (var openFinishing = conn.CreateCommand())
        {
            openFinishing.Transaction = tx;
            openFinishing.CommandText = @"
            UPDATE tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            SET t.Tasks_Status = 1
            WHERE t.BatchProduct_ID = @bpId
            AND t.IsActive = 1
            AND ts.Step_Type = 3
            AND t.Tasks_Status = 5;";

            openFinishing.Parameters.Add(new MySqlParameter("@bpId", batchProductId));

            await openFinishing.ExecuteNonQueryAsync();
        }
    }
}

private async Task CloseWorkSession(
    DbConnection conn,
    DbTransaction tx,
    int taskId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
UPDATE tasks_work_sessions
SET 
    EndTime = CURRENT_TIMESTAMP,
    DurationMinutes = TIMESTAMPDIFF(MINUTE, StartTime, CURRENT_TIMESTAMP)
WHERE Task_ID = @taskId
  AND EndTime IS NULL;";

    cmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

    await cmd.ExecuteNonQueryAsync();
}

private async Task CreateChildPaintingTasksIfNeeded(
    DbConnection conn,
    DbTransaction tx,
    int batchProductId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
INSERT INTO tasks
(
    BatchProduct_ID,
    TopPartStep_ID,
    Tasks_Status,
    Qty_Done,
    IsActive
)
SELECT
    bp.ID,
    ts.ID,
    5,
    0,
    1
FROM batches_products bp

JOIN tasks t0
    ON t0.BatchProduct_ID = bp.ID

JOIN toppartsteps ts0
    ON ts0.ID = t0.TopPartStep_ID

JOIN toppartsteps ts
    ON ts.ProductToPart_ID = ts0.ProductToPart_ID
   AND ts.IsPainting = 1

WHERE bp.ID = @bpId
AND bp.ProductToPart_ID IS NOT NULL

AND EXISTS (
    SELECT 1
    FROM tasks tf
    JOIN toppartsteps tsf
        ON tsf.ID = tf.TopPartStep_ID
    WHERE tf.BatchProduct_ID = bp.ID
      AND tf.Tasks_Status = 3
      AND tsf.IsFinal = 1
      AND tsf.IsPainting = 0
)

AND NOT EXISTS (
    SELECT 1
    FROM tasks t
    WHERE t.BatchProduct_ID = bp.ID
      AND t.TopPartStep_ID = ts.ID
      AND t.IsActive = 1
);";

    cmd.Parameters.Add(
        new MySqlParameter("@bpId", batchProductId));

    await cmd.ExecuteNonQueryAsync();
}

private async Task CreateRootPaintingTasksIfNeeded(
    DbConnection conn,
    DbTransaction tx,
    int rootId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
INSERT INTO tasks
(
    BatchProduct_ID,
    TopPartStep_ID,
    Tasks_Status,
    Qty_Done,
    IsActive
)
SELECT DISTINCT
    bp.ID,
    ts.ID,
    5,
    0,
    1
FROM batches_products bp

JOIN tasks t0
    ON t0.BatchProduct_ID = bp.ID

JOIN toppartsteps ts0
    ON ts0.ID = t0.TopPartStep_ID

JOIN toppartsteps ts
    ON ts.ProductToPart_ID = ts0.ProductToPart_ID
   AND ts.IsPainting = 1

WHERE
(
    bp.ID = @rootId
    OR bp.ParentBatchProduct_ID = @rootId
)

AND bp.ProductToPart_ID IS NOT NULL

AND EXISTS (
    SELECT 1
    FROM tasks tf
    JOIN toppartsteps tsf
        ON tsf.ID = tf.TopPartStep_ID
    WHERE tf.BatchProduct_ID = bp.ID
      AND tf.Tasks_Status = 3
      AND tsf.IsFinal = 1
      AND tsf.IsPainting = 0
)

AND NOT EXISTS (
    SELECT 1
    FROM tasks t
    WHERE t.BatchProduct_ID = bp.ID
      AND t.TopPartStep_ID = ts.ID
      AND t.IsActive = 1
);";

    cmd.Parameters.Add(
        new MySqlParameter("@rootId", rootId));

    await cmd.ExecuteNonQueryAsync();
}

}

}