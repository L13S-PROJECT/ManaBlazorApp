using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using System.Data;
using MySqlConnector;


namespace ManiApi.Services
{
    public class TaskService
    {
        private readonly AppDbContext _db;

        public TaskService(AppDbContext db)
        {
            _db = db;
        }


public async Task<int> GetWorkCenterId(int empId)
{
    return await _db.Employees
        .Where(e => e.Id == empId)
        .Select(e => e.WorkCentrTypeID ?? 0)
        .FirstOrDefaultAsync();
}
        

    public async Task<List<TaskRowDto>> GetForEmployee(int empId)
{
    Console.WriteLine($"GetForEmployee -> empId: {empId}");
    var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

    int workCenterId = await GetWorkCenterId(empId);
    Console.WriteLine($"WorkCenterId: {workCenterId}");

    await using var cmd = conn.CreateCommand();
    cmd.CommandType = CommandType.Text;
    cmd.CommandText = @"
SELECT
  t.ID,                      -- 0 TaskId
  t.Tasks_Priority,          -- 1 Priority
  t.Tasks_Push,
  bp.is_priority AS BatchPriority,
  bp.Priority AS BatchOrder,
  t.Tasks_Status,            -- 2 Status
  CASE
    WHEN bp.is_priority = 1 AND t.Tasks_Priority = 1 THEN 3
    WHEN bp.is_priority = 1 AND t.Tasks_Priority = 0 THEN 2
    WHEN bp.is_priority = 0 AND t.Tasks_Priority = 1 THEN 1
    ELSE 0
END AS PriorityLevel,

  t.Started_At,              -- 3 StartedAt
  t.Finished_At,             -- 4 FinishedAt

    t.Is_Comment_For_Employee, -- 5 IsCommentForEmployee
    t.Tasks_Comment AS Comment, -- 6 Comment    

  p.Product_Name,            -- 7 ProductName
  tp.TopPart_Name,           -- 8 PartName
    ts.ProductToPart_ID,
ts.Step_Name,

ts.Estimated_Minutes,

(
    SELECT COALESCE(SUM(s.DurationMinutes),0)
    FROM tasks_work_sessions s
    WHERE s.Task_ID = t.ID
) AS ActualMinutes,

(
    CASE 
        WHEN ts.Step_Type IN (1,2) THEN 
    bp.Planned_Qty * ts.Estimated_Minutes
        WHEN ts.Step_Type = 3 THEN t.Qty_Done * ts.Estimated_Minutes
        ELSE bp.Planned_Qty * ts.Estimated_Minutes
    END
) AS EstimatedTotalMinutes,

(
    SELECT COALESCE(SUM(ts2.Estimated_Minutes),0)
    FROM toppartsteps ts2
    WHERE ts2.ProductToPart_ID = ts.ProductToPart_ID
      AND ts2.Step_Order < ts.Step_Order
) AS EstimatedStartMinutes,

b.Batches_Code,

  COALESCE(t.Qty_Done, 0) AS DoneForTask, -- 11 Done
t.Assigned_To,
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
END AS PlannedQty,
  COALESCE(ts.Step_Order, 0) AS StepOrder, -- 11 soļa secība    
  ts.Step_Type              AS StepType,       -- 12 (Detailed/Assembly/Finishing)
  b.ID                      AS BatchId,       -- 13 (batches.ID)
  bp.Version_Id             AS VersionId,     -- 14 (versions.ID)
  bp.ID                     AS BatchProductId -- 15  (batches_products.ID)

FROM tasks t
JOIN batches_products bp   ON bp.ID  = t.BatchProduct_ID AND bp.IsActive = 1
JOIN versions v   ON v.ID   = bp.Version_Id AND v.IsActive = 1
JOIN products p   ON p.ID   = v.Product_ID AND p.IsActive = 1
JOIN batches          b    ON b.ID   = bp.Batch_Id       AND b.IsActive  = 1
JOIN toppartsteps     ts   ON ts.ID  = t.TopPartStep_ID
JOIN producttopparts  ptp  ON ptp.ID = ts.ProductToPart_ID
JOIN toppart          tp   ON tp.ID  = ptp.TopPart_ID
WHERE t.IsActive = 1
AND (
    -- 1) Mani taski (vienmēr redzu)
    t.Assigned_To = @empId

    OR

    -- 2) Taski, ko es jau daru
    (t.Tasks_Status = 2 AND t.Claimed_By = @empId)

    OR

    -- 3) Brīvie taski manā workcentrā
    (
        t.Tasks_Status = 1
        AND t.Assigned_To IS NULL
        AND ts.WorkCentr_ID = @workCenterId
    )
)

ORDER BY
  CASE
      WHEN t.Tasks_Push = 1 THEN 0
      WHEN bp.is_priority = 1 AND t.Tasks_Priority = 1 THEN 1
      WHEN bp.is_priority = 1 AND t.Tasks_Priority = 0 THEN 2
      WHEN bp.is_priority = 0 AND t.Tasks_Priority = 1 THEN 3
      ELSE 4
  END,
  bp.Priority,
  ts.Step_Order,
  t.ID;
";


    var pEmp = cmd.CreateParameter();
    pEmp.ParameterName = "@empId";
    pEmp.Value = empId;
    cmd.Parameters.Add(pEmp);

    var pWc = cmd.CreateParameter();
    pWc.ParameterName = "@workCenterId";
    pWc.Value = workCenterId;
    cmd.Parameters.Add(pWc);

    Console.WriteLine($"SQL executed, reading tasks for empId={empId}");
    var rawTasks = new List<TaskRowDto>(256);

    await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess).ConfigureAwait(false);
    while (await r.ReadAsync().ConfigureAwait(false))
    {
        rawTasks.Add(new TaskRowDto
        {
            TaskId = r.GetInt32(0),
            Priority = r.IsDBNull(1) ? (byte)0 : r.GetByte(1),
            Tasks_Push = !r.IsDBNull(2) && r.GetBoolean(2),
            BatchPriority = r.GetBoolean(3),
            Status = r.GetInt32(5),
            PriorityLevel = r.IsDBNull(6) ? 0 : r.GetInt32(6),
            StartedAt = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7),
            FinishedAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
            IsCommentForEmployee = !r.IsDBNull(9) && r.GetBoolean(9),
            Comment = r.IsDBNull(10) ? null : r.GetString(10),
            ProductName = r.IsDBNull(11) ? null : r.GetString(11),
            PartName = r.IsDBNull(12) ? null : r.GetString(12),
            ProductToPartId = r.IsDBNull(13) ? 0 : r.GetInt32(13),
            StepName = r.IsDBNull(14) ? null : r.GetString(14),
            EstimatedMinutes = r.IsDBNull(15) ? 0 : r.GetInt32(15),
            ActualMinutes = r.IsDBNull(16) ? 0 : r.GetInt32(16),
            EstimatedTotalMinutes = r.IsDBNull(17) ? 0 : r.GetInt32(17),
            EstimatedStartMinutes = r.IsDBNull(18) ? 0 : r.GetInt32(18),
            BatchCode = r.IsDBNull(19) ? null : r.GetString(19),
            Done = r.IsDBNull(20) ? 0 : r.GetInt32(20),
            Assigned_To = r.IsDBNull(21) ? (int?)null : r.GetInt32(21),
            Planned = r.IsDBNull(22) ? 0 : r.GetInt32(22),
            StepOrder = r.IsDBNull(23) ? 0 : r.GetInt32(23),
            StepType = r.IsDBNull(24) ? 0 : r.GetInt32(24),
            BatchId = r.IsDBNull(25) ? 0 : r.GetInt32(25),
            VersionId = r.IsDBNull(26) ? 0 : r.GetInt32(26),
            BatchProductId = r.IsDBNull(27) ? 0 : r.GetInt32(27),
        });
    }

    var result = rawTasks.ToList();
    var allTasks = await GetAllActiveTasks();

    // apvienojam Parent+Child uz root līmeni (tikai display/loģikai)
result = result
    .GroupBy(t =>
        allTasks.FirstOrDefault(x => x.BatchProductId == t.BatchProductId)?.RootId
        ?? t.BatchProductId)
    .Select(g => g
        .OrderBy(t => t.StepOrder)
        .ThenBy(t => t.TaskId)
        .First())
    .ToList();



Console.WriteLine($"Final tasks count for empId={empId}: {result.Count}");

foreach (var t in result)
{
    var rootId = allTasks
        .FirstOrDefault(x => x.BatchProductId == t.BatchProductId)?.RootId
        ?? t.BatchProductId;

    var hasPrevNotFinished = allTasks.Any(prev =>
        prev.RootId == rootId &&
        prev.StepOrder < t.StepOrder &&
        prev.Status != 3);

    var hasHigherPriority = allTasks.Any(other =>
            other.TaskId != t.TaskId &&
            other.Status == 1 &&
            !allTasks.Any(prev =>
                prev.RootId == rootId &&
                prev.StepOrder < other.StepOrder &&
                prev.Status != 3) &&

            (
                (other.Tasks_Push && !t.Tasks_Push)
                || (other.BatchPriority && !t.BatchPriority)
                || (other.BatchPriority == t.BatchPriority && other.Priority > t.Priority)
            )
        );

t.CanStart = !hasPrevNotFinished && !hasHigherPriority;
}

return result.OrderBy(t => t.Tasks_Push ? 0 : 1)
             .ThenBy(t => t.BatchPriority ? 0 : 1)
             .ThenByDescending(t => t.Priority)
             .ThenBy(t => t.StepOrder)
             .ThenBy(t => t.TaskId)
             .ToList();
}

public async Task<List<TaskRowDto>> GetAllActiveTasks()
{
    await using var conn = new MySqlConnector.MySqlConnection(_db.Database.GetConnectionString());
       if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

    await using var cmd = conn.CreateCommand();
    cmd.CommandType = CommandType.Text;
        cmd.CommandText = @"
    SELECT 
        t.ID,
        t.BatchProduct_ID,
        ts.Step_Order,
        t.Tasks_Status,
        bp.is_priority AS BatchPriority,
        t.Assigned_To,
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
        END AS RootId
    FROM tasks t
    JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    WHERE t.IsActive = 1
      AND t.Tasks_Status <> 4
";

    var list = new List<TaskRowDto>(128);

    await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
    while (await r.ReadAsync())
    {
        list.Add(new TaskRowDto
            {
                TaskId = r.GetInt32(0),
                BatchProductId = r.GetInt32(1),
                StepOrder = r.GetInt32(2),
                Status = r.GetInt32(3),
                BatchPriority = r.GetBoolean(4),
                Assigned_To = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                RootId = r.GetInt32(6),
                CanStart = true
            });
    }
    await r.DisposeAsync();
    return list;
}

public async Task<(bool Success, string? Error)> ClaimTask(int taskId, int empId)
{
    if (taskId <= 0 || empId <= 0)
        return (false, "Bad request");

    var conn = _db.Database.GetDbConnection();
    if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

    await using var tx = await conn.BeginTransactionAsync();

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
    Console.WriteLine("BLOCKED BY PRIORITY RULE");
    await tx.RollbackAsync();
    Console.WriteLine($"DEBUG CLAIM: taskId={taskId}, emp={empId}");
    Console.WriteLine($"curIsPriority={currentIsPriority}, curBatchOrder={currentBatchOrder}, curStepOrder={currentStepOrder}");
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
  AND (
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
    ) = (
        SELECT 
            CASE 
                WHEN EXISTS (
                    SELECT 1
                    FROM batches_products bp2
                    WHERE bp2.Batch_Id = bp3.Batch_Id
                      AND bp2.Version_Id = bp3.Version_Id
                      AND bp2.ProductToPart_ID IS NULL
                      AND bp2.IsActive = 1
                )
                THEN (
                    SELECT bp2.ID
                    FROM batches_products bp2
                    WHERE bp2.Batch_Id = bp3.Batch_Id
                      AND bp2.Version_Id = bp3.Version_Id
                      AND bp2.ProductToPart_ID IS NULL
                      AND bp2.IsActive = 1
                    LIMIT 1
                )
                ELSE bp3.ID
            END
        FROM batches_products bp3
        WHERE bp3.ID = (
            SELECT BatchProduct_ID FROM tasks WHERE ID = @taskId
        )
    )
LIMIT 1;";

    checkPrev.Parameters.Add(new MySqlParameter("@taskId", taskId));
    checkPrev.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));

    var hasPrev = await checkPrev.ExecuteScalarAsync() != null;
    
    if (hasPrev)
    {
        await tx.RollbackAsync();
        return (false, "Iepriekšējais solis nav pabeigts.");
    }
}

var scenario = DetectScenario(hasRoot, currentStepOrder);

    // 2) Pārejam uz statusu 2 šim taskam
    await using (var upd = conn.CreateCommand())
    {
        upd.Transaction = tx;
        if (scenario == TaskScenario.B_Root || scenario == TaskScenario.C_Child)
{
    upd.CommandText = @"UPDATE tasks 
   SET Tasks_Status = 2, 
       Claimed_By   = @emp,
       Started_At   = CURRENT_TIMESTAMP
 WHERE ID IN (
    SELECT t2.ID
    FROM tasks t2
    JOIN batches_products bp2 ON bp2.ID = t2.BatchProduct_ID
    JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID
    WHERE t2.IsActive = 1
      AND t2.Tasks_Status IN (1)
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

)
   AND Tasks_Status = 1 
   AND IsActive = 1;";
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
  AND ts2.ProductToPart_ID = (
    SELECT ts.ProductToPart_ID
    FROM tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    WHERE t.ID = @taskId
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
    var conn = _db.Database.GetDbConnection();
if (conn.State != ConnectionState.Open)
    {
        await conn.OpenAsync();
    }
await using var tx = await conn.BeginTransactionAsync();

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
    p.Value = taskId;
    info.Parameters.Add(p);

    await using var rr = await info.ExecuteReaderAsync();
    if (!await rr.ReadAsync())
    {
        await tx.RollbackAsync();
        return (false, "Uzdevuma dati nav atrasti.");
    }

    stepType       = rr.GetInt32(0);
    qtyPerProduct  = rr.GetInt32(1);
    plannedQty     = rr.GetInt32(2);
    currentDone    = rr.GetInt32(3);
    batchProductId = rr.GetInt32(4);
    versionId      = rr.IsDBNull(5) ? 0 : rr.GetInt32(5);
    var finishingPlannedQty = rr.IsDBNull(6) ? 0 : rr.GetInt32(6);
    ralColorId = rr.IsDBNull(7) ? null : rr.GetInt32(7);

    if (stepType == 3 && finishingPlannedQty > 0)
    {
        plannedQty = finishingPlannedQty;
    }
}

int rootId = 0;
bool hasRoot = false;;

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
    var qtyDone = plannedQty * qtyPerProduct;
    
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
AND t.BatchProduct_ID IN (
    SELECT bp3.ID
    FROM batches_products bp3
    JOIN batches_products root ON root.ID = @rootId
    WHERE bp3.IsActive = 1
      AND bp3.Batch_Id = root.Batch_Id
      AND bp3.Version_Id = root.Version_Id
);";

        var p1 = upd.CreateParameter();
        p1.ParameterName = "@qtyDone";
        p1.Value = qtyDone;
        upd.Parameters.Add(p1);

        var p2 = upd.CreateParameter();
        p2.ParameterName = "@id";
        p2.Value = taskId;
        upd.Parameters.Add(p2);
        upd.Parameters.Add(new MySqlParameter("@rootId", rootId));
        
        await upd.ExecuteNonQueryAsync();
    }

  }

else if (scenario == TaskScenario.A_Parent && stepType == 2 && batchProductId > 0 && versionId > 0)
{
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

   
}

else if (scenario == TaskScenario.A_Parent)
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

        var p = upd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = taskId;
        upd.Parameters.Add(p);

        await upd.ExecuteNonQueryAsync();
    }

    // 2) FINISHING -> STOCK kustība
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

    var p = closeSession.CreateParameter();
    p.ParameterName = "@taskId";
    p.Value = taskId;
    closeSession.Parameters.Add(p);

    await closeSession.ExecuteNonQueryAsync();
}

    await tx.CommitAsync();
    return (true, null); // newStatus izmantosim nākamajos soļos
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

}

}