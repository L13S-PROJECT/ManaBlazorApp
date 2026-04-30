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
    if (taskId <= 0 || empId <= 0)
        return (false, "Bad request");
    
// 🔒 pārbaudām vai task vispār drīkst sākt (UI loģika serverī)
        var availableTasks = await GetForEmployee(empId);

// 🔑 atrodam VISUS raw taskus šai grupai
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
                        {
                            Console.WriteLine($"BLOCK: not first available. clicked={taskId}, first={firstAvailable?.TaskId}");
                            return (false, "Drīkst sākt tikai nākamo prioritāro darbu.");
                        }

    Console.WriteLine($"CLAIM CALLED: taskId={taskId}, empId={empId}");

    var conn = _db.Database.GetDbConnection();
    if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

    await using var tx = await conn.BeginTransactionAsync();

   /* // Vienmēr pārejam uz root "master" tasku
await using (var rootFix = conn.CreateCommand())
{
    rootFix.Transaction = tx;
    rootFix.CommandText = @"
SELECT t2.ID
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches_products bp2 ON (
    bp2.ID = bp.ID 
    OR bp2.ParentBatchProduct_ID = bp.ID
    OR bp.ParentBatchProduct_ID = bp2.ID
)
JOIN tasks t2 ON t2.BatchProduct_ID = bp2.ID
JOIN toppartsteps ts ON ts.ID = t2.TopPartStep_ID
WHERE t.ID = @taskId
AND ts.Step_Order = (
    SELECT ts2.Step_Order
    FROM tasks t3
    JOIN toppartsteps ts2 ON ts2.ID = t3.TopPartStep_ID
    WHERE t3.ID = @taskId
)
ORDER BY 
    (t2.Tasks_Status = 2) DESC,
    (t2.Assigned_To = @emp) DESC,
    (bp.ParentBatchProduct_ID IS NULL) DESC
LIMIT 1;";

    rootFix.Parameters.Add(new MySqlParameter("@taskId", taskId));
    rootFix.Parameters.Add(new MySqlParameter("@emp", empId));

    var newId = await rootFix.ExecuteScalarAsync();

    if (newId != null)
        taskId = Convert.ToInt32(newId);
} */

    // 1) Vai šim darbiniekam jau nav cita aktīva darba (status 2)?
    await using (var chk = conn.CreateCommand())
    {
        chk.Transaction = tx;
        chk.CommandText = @"
SELECT COUNT(*) 
FROM tasks 
WHERE Claimed_By = @emp 
  AND Tasks_Status = 2 
  AND IsActive = 1;";

        var pEmp = chk.CreateParameter();
        pEmp.ParameterName = "@emp";
        pEmp.Value = empId;
        chk.Parameters.Add(pEmp);

        var cnt = Convert.ToInt32(await chk.ExecuteScalarAsync());
        if (cnt > 0)
            {
                Console.WriteLine("BLOCK: already has active task");
                await tx.RollbackAsync();
                return (false, "Jau ir iesākts cits darbs.");
            }
    }

 

    int currentIsPriority = 0;
int currentBatchOrder = 0;
int currentStepOrder = 0;

await using (var cur = conn.CreateCommand())
{
    cur.Transaction = tx;
    cur.CommandText = @"
SELECT 
    bp.is_priority,
    bp.Priority,
    ts.Step_Order
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.ID = @id;";

    var p = cur.CreateParameter();
    p.ParameterName = "@id";
    p.Value = taskId;
    cur.Parameters.Add(p);

    await using var r = await cur.ExecuteReaderAsync();
    if (await r.ReadAsync())
    {
        currentIsPriority = r.GetBoolean(0) ? 1 : 0;
        currentBatchOrder = r.GetInt32(1);
        currentStepOrder = r.GetInt32(2);
    }
}

await using (var checkOrder = conn.CreateCommand())
{
    checkOrder.Transaction = tx;

    checkOrder.CommandText = @"
SELECT 1
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive = 1
  AND bp.IsActive = 1
  AND t.Tasks_Status = 2
  AND t.ID <> @taskId
  AND (t.Assigned_To = @emp OR t.Assigned_To IS NULL)
AND (
        (bp.is_priority = 1 AND @curIsPriority = 0)

     OR (bp.is_priority = @curIsPriority 
         AND bp.Priority = @curBatchOrder 
         AND bp.ID = (
            SELECT BatchProduct_ID
            FROM tasks
            WHERE ID = @taskId
         )
         AND ts.Step_Order < @curStepOrder)
)
LIMIT 1;";

    var pTask = checkOrder.CreateParameter();
    pTask.ParameterName = "@taskId";
    pTask.Value = taskId;
    checkOrder.Parameters.Add(pTask);

    var pEmp = checkOrder.CreateParameter();
    pEmp.ParameterName = "@emp";
    pEmp.Value = empId;
    checkOrder.Parameters.Add(pEmp);

    var p1 = checkOrder.CreateParameter();
    p1.ParameterName = "@curIsPriority";
    p1.Value = currentIsPriority;
    checkOrder.Parameters.Add(p1);

    var p2 = checkOrder.CreateParameter();
    p2.ParameterName = "@curBatchOrder";
    p2.Value = currentBatchOrder;
    checkOrder.Parameters.Add(p2);

    var p3 = checkOrder.CreateParameter();
    p3.ParameterName = "@curStepOrder";
    p3.Value = currentStepOrder;
    checkOrder.Parameters.Add(p3);

    var higherExists = await checkOrder.ExecuteScalarAsync() != null;

if (higherExists)
{
    Console.WriteLine("BLOCK: higher priority");
    await tx.RollbackAsync();
    return (false, "Ir augstākas prioritātes darbs.");
}

}

bool hasRoot = false;

int rootId = 0;

await using (var rootCmd = conn.CreateCommand())
{
    rootCmd.Transaction = tx;
    rootCmd.CommandText = @"
SELECT 
    CASE 
        WHEN EXISTS (
            SELECT 1
            FROM batches_products bp2
            WHERE bp2.ID = bp.ID
            OR bp2.ParentBatchProduct_ID = bp.ID
            OR bp.ParentBatchProduct_ID = bp2.ID
        )
        THEN 1
        ELSE 0
    END,
COALESCE(bp.ParentBatchProduct_ID, bp.ID)
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
WHERE t.ID = @taskId;";

    rootCmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

    await using var r = await rootCmd.ExecuteReaderAsync();
   

        if (await r.ReadAsync())
        {
            hasRoot = r.GetInt32(0) == 1;
            rootId = r.GetInt32(1);
        }
        
}

// 🔒 STEP secības validācija (server-side)
await using (var checkPrev = conn.CreateCommand())
{
    checkPrev.Transaction = tx;

    checkPrev.CommandText = @"
SELECT 1
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status <> 3
  AND ts.Step_Order < @curStepOrder
  AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)
  AND ts.ProductToPart_ID = (
        SELECT ts2.ProductToPart_ID
        FROM tasks t2
        JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
        WHERE t2.ID = @taskId
    )
  AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId
LIMIT 1;";

    checkPrev.Parameters.Add(new MySqlParameter("@rootId", rootId));
    checkPrev.Parameters.Add(new MySqlParameter("@taskId", taskId));
    checkPrev.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));

    var hasPrev = await checkPrev.ExecuteScalarAsync() != null;
    
    if (hasPrev)
{
    Console.WriteLine("BLOCK: previous step not finished");
    await tx.RollbackAsync();
    return (false, "Iepriekšējais solis nav pabeigts.");
}
}

int stepTypeForScenario = 0;

await using (var stepCmd = conn.CreateCommand())
{
    stepCmd.Transaction = tx;
    stepCmd.CommandText = @"
SELECT ts.Step_Type
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.ID = @taskId;";

    stepCmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

    var obj = await stepCmd.ExecuteScalarAsync();
    if (obj != null)
        stepTypeForScenario = Convert.ToInt32(obj);
}

var scenario = DetectScenario(hasRoot, stepTypeForScenario);

// 🔒 Atļauts sākt tikai pašu pirmo pieejamo tasku (server-side)
var allTasks = await GetAllActiveTasks();

Console.WriteLine($"CLICKED TASK ID: {taskId}");

var currentTask = allTasks.FirstOrDefault(t => t.TaskId == taskId);

if (currentTask == null)
{
    await tx.RollbackAsync();
    return (false, "Task not found.");
}

/* // tas pats loģikas princips kā UI
var firstAvailable = allTasks
    .Where(t => t.Status == 1)
    .Where(t => t.Assigned_To == empId || t.Assigned_To == null)
    .Where(t => !allTasks.Any(prev =>
        prev.RootId == t.RootId &&
        prev.StepOrder < t.StepOrder &&
        prev.Status != 3))
    .OrderBy(t => t.Tasks_Push ? 0 : 1)
    .ThenByDescending(t => t.BatchPriority ? 1 : 0)
    .ThenByDescending(t => t.Priority)
    .ThenBy(t => t.StepOrder)
    .ThenBy(t => t.TaskId)
    .FirstOrDefault();

Console.WriteLine($"FIRST AVAILABLE: {firstAvailable?.TaskId}");
Console.WriteLine($"CLICKED: {taskId}");

if (firstAvailable == null || firstAvailable.TaskId != taskId)
{
    Console.WriteLine($"BLOCK: firstAvailable={firstAvailable?.TaskId}, current={taskId}");
    Console.WriteLine($"DEBUG -> firstAvailable: {firstAvailable?.TaskId}, clicked: {taskId}");
    await tx.RollbackAsync();
    return (false, "Drīkst sākt tikai pirmo pieejamo darbu.");
}*/

// 🔒 Root gadījumā pārbaudām VISUS step taskus
if (scenario == TaskScenario.B_Root)
{
    await using var checkRootPrev = conn.CreateCommand();
    checkRootPrev.Transaction = tx;

    checkRootPrev.CommandText = @"
SELECT 1
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status <> 3
  AND ts.Step_Order < @curStepOrder
  AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)
    AND ts.ProductToPart_ID = (
        SELECT ts2.ProductToPart_ID
        FROM tasks t2
        JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
        WHERE t2.ID = @taskId
    )
AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId
LIMIT 1;";

    checkRootPrev.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));
    checkRootPrev.Parameters.Add(new MySqlParameter("@rootId", rootId));

    var hasPrevRoot = await checkRootPrev.ExecuteScalarAsync() != null;

    if (hasPrevRoot)
    {
        await tx.RollbackAsync();
        return (false, "Root: iepriekšējais solis nav pabeigts.");
    }
}

    // 2) Pārejam uz statusu 2 šim taskam
    await using (var upd = conn.CreateCommand())
    {
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
    upd.CommandText = @"UPDATE tasks 
   SET Tasks_Status = 2, 
       Claimed_By   = @emp,
       Started_At   = CURRENT_TIMESTAMP
 WHERE ID = @taskId
   AND Tasks_Status = 1 
   AND IsActive = 1;";
}

        var pEmp = upd.CreateParameter();
        pEmp.ParameterName = "@emp";
        pEmp.Value = empId;
        upd.Parameters.Add(pEmp);

        var pId = upd.CreateParameter();
        pId.ParameterName = "@taskId";
        pId.Value = taskId;
        upd.Parameters.Add(pId);
        upd.Parameters.Add(new MySqlParameter("@rootId", rootId));
        upd.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));
        upd.Parameters.Add(new MySqlParameter("@scenario", scenario == TaskScenario.B_Root ? 1 : 0));

        var affected = await upd.ExecuteNonQueryAsync();
        if (affected == 0)
        {
            await tx.RollbackAsync();
            return (false, "Darbs vairs nav pieejams.");
        }
    }

    // 3) Ja šis ir FINISHING solis (Step_Type = 3) un ir norādīts apjoms,
    //    veicam kustību ASSEMBLY -> FINISHING stock_movements (idempotenti).
        int stepType = 0;
        int batchProductId = 0;
        int versionId = 0;
        int finishingQty = 0;
        int? ralColorId = null;

    await using (var info = conn.CreateCommand())
    {
        info.Transaction = tx;
        info.CommandText = @"
SELECT 
    ts.Step_Type,
    t.BatchProduct_ID,
    bp.Version_Id,
    COALESCE(t.Qty_Done, 0) AS FinishingQty,
    t.RAL_Color_ID
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID AND bp.IsActive = 1
WHERE t.ID = @id
  AND t.IsActive = 1;";

        var p = info.CreateParameter();
        p.ParameterName = "@id";
        p.Value = taskId;
        info.Parameters.Add(p);

        await using var r = await info.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            stepType = r.GetInt32(0);
            batchProductId = r.GetInt32(1);
            versionId = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            finishingQty = r.IsDBNull(3) ? 0 : r.GetInt32(3);
            ralColorId = r.IsDBNull(4) ? null : r.GetInt32(4);
        }
    }

    // Tikai, ja tas ir Finishing solis un ir jēgpilns apjoms
   // Finishing kustība tiek veikta OpenFinishing()
// Claim šeit tikai maina statusu
await using (var session = conn.CreateCommand())
{
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
    
    await session.ExecuteNonQueryAsync();
}

    await tx.CommitAsync();
    return (true, null);
}

public async Task<(bool Success, string? Error)> FinishTask(int taskId, int? qtyDoneAdd)
{
    Console.WriteLine($"FINISH CALLED: taskId={taskId}");

    var conn = _db.Database.GetDbConnection();
if (conn.State != ConnectionState.Open)
    {
        await conn.OpenAsync();
    }
await using var tx = await conn.BeginTransactionAsync();

taskId = await ResolveRootTaskId(conn, tx, taskId);

int currentStatus;

// 1) Nolasām statusu
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
    p.Value = taskId;
    cmd.Parameters.Add(p);

    var obj = await cmd.ExecuteScalarAsync();
    if (obj == null || obj == DBNull.Value)
    {
        await tx.RollbackAsync();
        return (false, "Uzdevums nav atrasts vai ir neaktīvs.");
    }

    currentStatus = Convert.ToInt32(obj);
}

// 2) Atļaujam tikai status = 2
if (currentStatus != 2)
{
    await tx.RollbackAsync();
    return (false, "Pabeigt drīkst tikai uzdevumu ar statusu 'Procesā'.");
}

int stepType;
int productToPartId;
int currentStepOrder;
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
    ts.Step_Order,
    ts.ProductToPart_ID,
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
    p.Value = taskId;
    info.Parameters.Add(p);

    await using var rr = await info.ExecuteReaderAsync();
    if (!await rr.ReadAsync())
    {
        await tx.RollbackAsync();
        return (false, "Uzdevuma dati nav atrasti.");
    }

    stepType         = rr.GetInt32(0);
    currentStepOrder = rr.GetInt32(1);
    productToPartId = rr.GetInt32(2);
    qtyPerProduct    = rr.GetInt32(3);
    plannedQty       = rr.GetInt32(4);
    currentDone      = rr.GetInt32(5);
    batchProductId   = rr.GetInt32(6);
    versionId        = rr.IsDBNull(7) ? 0 : rr.GetInt32(7);
    var finishingPlannedQty = rr.IsDBNull(8) ? 0 : rr.GetInt32(8);
    ralColorId       = rr.IsDBNull(9) ? null : rr.GetInt32(9);

    if (stepType == 3 && finishingPlannedQty > 0)
    {
        plannedQty = finishingPlannedQty;
    }
}

int rootId = 0;
bool hasRoot = false;

await using (var rootCmd = conn.CreateCommand())
{
    rootCmd.Transaction = tx;
    rootCmd.CommandText = @"
SELECT 
    CASE 
        WHEN EXISTS (
            SELECT 1
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.ProductToPart_ID IS NULL
              AND bp2.IsActive = 1
        )
        THEN 1 ELSE 0
    END,
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
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
WHERE t.ID = @taskId;";

    rootCmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

    await using var r = await rootCmd.ExecuteReaderAsync();
    if (await r.ReadAsync())
    {
        hasRoot = r.GetInt32(0) == 1;
        rootId = r.GetInt32(1);
    }
}

var scenario = DetectScenario(hasRoot, stepType);

// Detailed (StepType = 1) → pabeidzam visu
if (scenario == TaskScenario.B_Root)
{
    var qtyDone = stepType == 1
        ? plannedQty * qtyPerProduct
        : plannedQty;
    
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

        var p1 = upd.CreateParameter();
        p1.ParameterName = "@qtyDone";
        p1.Value = qtyDone;
        upd.Parameters.Add(p1);

        upd.Parameters.Add(new MySqlParameter("@rootId", rootId));
        upd.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));

        await upd.ExecuteNonQueryAsync();
    }

    //  pārbaudām vai visi DETAIL step pabeigti šim BatchProduct
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

            -- ❗ tikai Parent (nav child rindas)
            AND bp.ProductToPart_ID IS NULL

            -- tas pats root
            AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;"; //  tikai Parent

        openAssembly.Parameters.Add(new MySqlParameter("@rootId", rootId));

        await openAssembly.ExecuteNonQueryAsync();
    }
}

  }

else if (scenario == TaskScenario.A_Parent)
{   
    bool detailFinished = false;
    if (stepType == 1)
    {
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
            detailFinished = await CheckDetailFinished(conn, tx, batchProductId);
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

        -- ❗ tikai Parent (nav child rindas)
        AND bp.ProductToPart_ID IS NULL

        -- tas pats root
        AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;";

    openAssembly.Parameters.Add(new MySqlParameter("@rootId", rootId));

    await openAssembly.ExecuteNonQueryAsync();
}
}
}
else if (stepType == 2)
{
    detailFinished = await CheckDetailFinished(conn, tx, rootId);
    await using (var upd = conn.CreateCommand())
    {
        upd.Transaction = tx;
        upd.CommandText = @"
        UPDATE tasks
        SET Tasks_Status = 3,
            Finished_At  = CURRENT_TIMESTAMP
        WHERE ID = @id;";

        upd.Parameters.Add(new MySqlParameter("@id", taskId));

        await upd.ExecuteNonQueryAsync();
    }

    int notFinishedAssembly = 0;

    await using (var cmdCheckAsm = conn.CreateCommand())
            {
                cmdCheckAsm.Transaction = tx;
                cmdCheckAsm.CommandText = @"
        SELECT COUNT(*)
            FROM tasks t
            JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            WHERE t.IsActive = 1
            AND ts.Step_Type = 2
            AND t.Tasks_Status <> 3
            AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;";

                var pBp = cmdCheckAsm.CreateParameter();
                pBp.ParameterName = "@rootId";
                pBp.Value = rootId;
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
                FROM stock_movements sm
                JOIN batches_products bp ON bp.ID = sm.BatchProduct_ID
                WHERE sm.Move_Type = 'ASSEMBLY'
                AND sm.IsActive = 1
                AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;";

            var pM = cmdCheckMove.CreateParameter();
                pM.ParameterName = "@rootId";
                pM.Value = rootId;
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

                var pBp2 = cmdMove.CreateParameter();
                pBp2.ParameterName = "@bpId";
                pBp2.Value = batchProductId;
                cmdMove.Parameters.Add(pBp2);

                var pQty = cmdMove.CreateParameter();
                pQty.ParameterName = "@qty";
                pQty.Value = totalQty;
                cmdMove.Parameters.Add(pQty);

                var pTask = cmdMove.CreateParameter();
                pTask.ParameterName = "@taskId";
                pTask.Value = taskId;
                cmdMove.Parameters.Add(pTask);

                await cmdMove.ExecuteNonQueryAsync();
            }
        }
    }

bool isFinalStep = false;

await using (var checkFinal = conn.CreateCommand())
{
    checkFinal.Transaction = tx;
    checkFinal.CommandText = @"
SELECT ts.IsFinal
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.ID = @id;";

    checkFinal.Parameters.Add(new MySqlParameter("@id", taskId));

    var obj = await checkFinal.ExecuteScalarAsync();
    if (obj != null && obj != DBNull.Value)
        isFinalStep = Convert.ToBoolean(obj);
}

if (isFinalStep && detailFinished && notFinishedAssembly == 0)
{
    var qtyMove = currentDone;

    if (qtyMove > 0 && batchProductId > 0 && versionId > 0)
    {
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

            chk.Parameters.Add(new MySqlParameter("@taskId", taskId));
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
                mv.Parameters.Add(new MySqlParameter("@taskId", taskId));
                mv.Parameters.Add(new MySqlParameter("@ral", (object?)ralColorId ?? DBNull.Value));

                await mv.ExecuteNonQueryAsync();
            }
        }
    }
}

}
}

else if (scenario == TaskScenario.C_Child)
{
    // 1) Task -> Finished
    await using (var upd = conn.CreateCommand())
    {
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

    // pārbaudām vai esam sasnieguši FINAL step
bool isFinalStep = false;

await using (var checkFinal = conn.CreateCommand())
{
    checkFinal.Transaction = tx;
    checkFinal.CommandText = @"
SELECT ts.IsFinal
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.ID = @id;";

    checkFinal.Parameters.Add(new MySqlParameter("@id", taskId));

    var obj = await checkFinal.ExecuteScalarAsync();
    if (obj != null && obj != DBNull.Value)
        isFinalStep = Convert.ToBoolean(obj);
}

    if (detailFinished && versionId > 0 && isFinalStep)
    {
        int alreadyDone = 0;

        await using (var chk = conn.CreateCommand())
        {
            chk.Transaction = tx;
            chk.CommandText = @"
SELECT COUNT(*)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'DETAILED'
  AND IsActive = 1;";

            chk.Parameters.Add(new MySqlParameter("@bpId", batchProductId));

            alreadyDone = Convert.ToInt32(await chk.ExecuteScalarAsync());
        }

        if (alreadyDone == 0)
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

    closeSession.Parameters.Add(new MySqlParameter("@taskId", taskId));

    await closeSession.ExecuteNonQueryAsync();
}

await tx.CommitAsync();
return (true, null);
}

private enum TaskScenario
{
    A_Parent,      // Parasts (nav root)
    B_Root,        // Parent + Child kopā
    C_Child        // Tikai child
}

private TaskScenario DetectScenario(bool hasRoot, int stepType)
{
    // B → Parent + Child (root gadījums)
    if (hasRoot)
        return TaskScenario.B_Root;

    // C → Child-only (nav root, bet DETAIL solis)
    if (stepType == 1)
        return TaskScenario.C_Child;

    // A → Parasts Parent
    return TaskScenario.A_Parent;
}

private async Task<TaskScenario> DetectScenarioV2(int batchProductId, int rootId)
{
    await using var conn = new MySqlConnection(_db.Database.GetConnectionString());
    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    // cik batch_products ir zem šī root
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT COUNT(*)
FROM batches_products
WHERE IsActive = 1
AND (
        ID = @rootId
     OR ParentBatchProduct_ID = @rootId
);";

    cmd.Parameters.Add(new MySqlParameter("@rootId", rootId));

    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

    // vai šis ir parent
    bool isParent = batchProductId == rootId;

    if (count > 1)
        return TaskScenario.B_Root;

    if (!isParent)
        return TaskScenario.C_Child;

    return TaskScenario.A_Parent;
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

private async Task<int> ResolveRootTaskId(DbConnection conn, DbTransaction tx, int taskId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT t2.ID
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN tasks t2 ON (
        t2.BatchProduct_ID = bp.ID
     OR t2.BatchProduct_ID IN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.ParentBatchProduct_ID = bp.ID
        )
)
WHERE t.ID = @taskId
ORDER BY (t2.Tasks_Status = 2) DESC
LIMIT 1;";

    cmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

    var result = await cmd.ExecuteScalarAsync();

    return result != null ? Convert.ToInt32(result) : taskId;
}

private async Task<bool> CheckDetailFinished(DbConnection conn, DbTransaction tx, int rootId)
{
    return await IsDetailPhaseFinishedAll(conn, tx, rootId);
}

private async Task<bool> IsDetailPhaseFinishedAll(DbConnection conn, DbTransaction tx, int rootId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT COUNT(*)
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID

WHERE t.IsActive = 1

-- tikai DETAIL posms
AND ts.Step_Type = 1

-- ❗ ignorē krāsošanu
AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)

-- tikai līdz IsFinal (ieskaitot)
AND ts.Step_Order <= (
    SELECT MIN(ts2.Step_Order)
    FROM toppartsteps ts2
    WHERE ts2.ProductToPart_ID = ts.ProductToPart_ID
      AND ts2.IsFinal = 1
      AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)
)

-- tikai šim root (Parent + Child kopā)
AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId

-- meklējam NEpabeigtos
AND t.Tasks_Status <> 3;";

    cmd.Parameters.Add(new MySqlParameter("@rootId", rootId));

    var notFinished = Convert.ToInt32(await cmd.ExecuteScalarAsync());

    return notFinished == 0;
}

}

}