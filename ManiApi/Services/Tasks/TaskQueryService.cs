// Tasks/taskqueryservice.cs -> TaskQueryService klase ar GetForEmployee, GetAllActiveTasks, GetAllActiveTasks


using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using System.Data;
using MySqlConnector;
using System.Data.Common;
using ManiApi.Services.Tasks;


namespace ManiApi.Services.Tasks
{
    public class TaskQueryService
    {
        private readonly AppDbContext _db;

        public TaskQueryService(AppDbContext db)
        {
            _db = db;
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
        t.TopPartStep_ID,
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
        ts.IsFinal,
        ts.IsPainting,
        b.ID                      AS BatchId,       -- 13 (batches.ID)
        bp.Version_Id             AS VersionId,     -- 14 (versions.ID)
        bp.ID                     AS BatchProductId, -- 15  (batches_products.ID)
        COALESCE(bp.ParentBatchProduct_ID, bp.ID) AS RootId -- 16 RootId

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
            -- 1) Procesā (vienmēr augstākā prioritāte)
            (t.Tasks_Status = 2 AND t.Claimed_By = @empId)

            OR

            -- 2) Assigned uz mani
            (t.Assigned_To = @empId AND t.Tasks_Status = 1)

            OR

            -- 3) Brīvie workcenter uzdevumi
            (
                t.Assigned_To IS NULL
                AND t.Tasks_Status = 1
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
                    TopPartStepId = r.GetInt32(1),
                    Priority = r.IsDBNull(2) ? (byte)0 : r.GetByte(2),
                    Tasks_Push = !r.IsDBNull(3) && r.GetBoolean(3),
                    BatchPriority = r.GetBoolean(4),
                    Status = r.GetInt32(6),
                    PriorityLevel = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                    StartedAt = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
                    FinishedAt = r.IsDBNull(9) ? (DateTime?)null : r.GetDateTime(9),
                    IsCommentForEmployee = !r.IsDBNull(10) && r.GetBoolean(10),
                    Comment = r.IsDBNull(11) ? null : r.GetString(11),
                    ProductName = r.IsDBNull(12) ? null : r.GetString(12),
                    PartName = r.IsDBNull(13) ? null : r.GetString(13),
                    ProductToPartId = r.IsDBNull(14) ? 0 : r.GetInt32(14),
                    StepName = r.IsDBNull(15) ? null : r.GetString(15),
                    EstimatedMinutes = r.IsDBNull(16) ? 0 : r.GetInt32(16),
                    ActualMinutes = r.IsDBNull(17) ? 0 : r.GetInt32(17),
                    EstimatedTotalMinutes = r.IsDBNull(18) ? 0 : r.GetInt32(18),
                    EstimatedStartMinutes = r.IsDBNull(19) ? 0 : r.GetInt32(19),
                    BatchCode = r.IsDBNull(20) ? null : r.GetString(20),
                    Done = r.IsDBNull(21) ? 0 : r.GetInt32(21),
                    Assigned_To = r.IsDBNull(22) ? (int?)null : r.GetInt32(22),
                    Planned = r.IsDBNull(23) ? 0 : r.GetInt32(23),
                    StepOrder = r.IsDBNull(24) ? 0 : r.GetInt32(24),
                    StepType = r.IsDBNull(25) ? 0 : r.GetInt32(25),
                    IsFinal = !r.IsDBNull(26) && r.GetBoolean(26),
                    IsPainting = !r.IsDBNull(27) && r.GetBoolean(27),
                    BatchId = r.IsDBNull(28) ? 0 : r.GetInt32(28),
                    VersionId = r.IsDBNull(29) ? 0 : r.GetInt32(29),
                    BatchProductId = r.IsDBNull(30) ? 0 : r.GetInt32(30),
                    RootId = r.IsDBNull(31) ? 0 : r.GetInt32(31),
                });
            }
            await r.DisposeAsync(); 

            var result = rawTasks;

            var allTasks = await GetAllActiveTasks();

            // apvienojam Parent+Child uz root līmeni (tikai display/loģikai)

        Console.WriteLine($"Final tasks count for empId={empId}: {result.Count}");

        foreach (var t in result)
        {
                
                t.DisplayGroupId = t.RootId;

                Console.WriteLine($"DBG -> Task:{t.TaskId} Root:{t.RootId} BP:{t.BatchProductId} Step:{t.TopPartStepId} DG:{t.DisplayGroupId}");
                    
        var hasPrevNotFinished = allTasks.Any(prev =>
                prev.RootId == t.RootId &&
                prev.ProductToPartId == t.ProductToPartId &&
                prev.StepOrder < t.StepOrder &&
                prev.Status != 3 &&
                prev.Status != 5 &&
                !prev.IsPainting &&
                !prev.IsFinal
            );

        var isChildOnly = !allTasks.Any(x =>
            x.RootId == t.RootId &&
            x.ProductToPartId == 0
        );

        var hasHigherPriority = allTasks.Any(other =>
            other.TaskId != t.TaskId &&
            other.Status == 1 &&
            other.RootId == t.RootId &&
        (
            !isChildOnly
            || other.ProductToPartId != t.ProductToPartId
        ) &&

            !allTasks.Any(prev =>
                prev.RootId == other.RootId &&
                prev.ProductToPartId == other.ProductToPartId &&
                prev.StepOrder < other.StepOrder &&
                prev.Status != 3 &&
                !prev.IsPainting &&
                !prev.IsFinal
            ) &&

            (
                (other.Tasks_Push && !t.Tasks_Push)
                || (other.BatchPriority && !t.BatchPriority)
                || (other.BatchPriority == t.BatchPriority && other.Priority > t.Priority)
            )
        );

        t.CanStart = !hasPrevNotFinished && !hasHigherPriority;
        }

        result = result
            .GroupBy(t => new { t.DisplayGroupId, t.TopPartStepId })
            .Select(g =>
        {
            var totalPlanned = g.Sum(x => x.Planned);
            var totalDone = g.Sum(x => x.Done);

            TaskRowDto row;

            // 1. Procesā
            var inProgress = g.FirstOrDefault(x => x.Status == 2);
            if (inProgress != null)
            {
                row = inProgress;
            }
            else
            {
                // 2. Assigned
                var assigned = g
                    .Where(x => x.Assigned_To == empId)
                    .OrderBy(x => x.StepOrder)
                    .FirstOrDefault();

                if (assigned != null)
                {
                    row = assigned;
                }
                else
                {
                    // 3. CanStart
                    var canStart = g
                        .Where(x => x.CanStart == true)
                        .OrderBy(x => x.StepOrder)
                        .FirstOrDefault();

                    if (canStart != null)
                    {
                        row = canStart;
                    }
                    else
                    {
                        // 4. fallback
                        row = g
                            .OrderBy(x => x.StepOrder)
                            .First();
                    }
                }
            }

        return new TaskRowDto
        {
            TaskId = row.TaskId,
            TopPartStepId = row.TopPartStepId,
            Priority = row.Priority,
            Tasks_Push = row.Tasks_Push,
            BatchPriority = row.BatchPriority,
            Status = row.Status,
            PriorityLevel = row.PriorityLevel,
            StartedAt = row.StartedAt,
            FinishedAt = row.FinishedAt,
            IsCommentForEmployee = row.IsCommentForEmployee,
            Comment = row.Comment,
            ProductName = row.ProductName,
            PartName = row.PartName,
            ProductToPartId = row.ProductToPartId,
            StepName = row.StepName,
            EstimatedMinutes = row.EstimatedMinutes,
            ActualMinutes = row.ActualMinutes,
            EstimatedTotalMinutes = row.EstimatedTotalMinutes,
            EstimatedStartMinutes = row.EstimatedStartMinutes,
            BatchCode = row.BatchCode,
            Done = totalDone,              
            Assigned_To = row.Assigned_To,
            Planned = totalPlanned,        
            StepOrder = row.StepOrder,
            StepType = row.StepType,
            BatchId = row.BatchId,
            VersionId = row.VersionId,
            BatchProductId = row.BatchProductId,
            RootId = row.RootId,
            DisplayGroupId = row.DisplayGroupId,
            CanStart = row.CanStart
        };
        })
            .ToList();
            
        return result
            .OrderBy(t => t.Status == 2 ? 0 : 1) // procesā vienmēr pirmais
            .ThenBy(t => t.Tasks_Push ? 0 : 1)
            .ThenByDescending(t => t.Assigned_To == empId) //  Assigned uz mani = augstāk
            .ThenBy(t => t.PriorityLevel * -1) // augstāks PriorityLevel = augstāk
            .ThenBy(t => t.StepOrder)
            .ThenBy(t => t.TaskId)
            .ToList();
    }

public async Task<List<TaskRowDto>> GetAllActiveTasks()
        {
            var conn = _db.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync();
                }

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
                cmd.CommandText = @"
            SELECT 
                t.ID,
                t.TopPartStep_ID,
                t.BatchProduct_ID,
                ts.Step_Order,
                t.Tasks_Status,
                bp.is_priority AS BatchPriority,
                t.Assigned_To,
                t.Tasks_Priority,
                t.Tasks_Push,
                COALESCE(bp.ParentBatchProduct_ID, bp.ID) AS RootId,
                ts.ProductToPart_ID,
                ts.IsPainting
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
                        TopPartStepId = r.GetInt32(1),
                        BatchProductId = r.GetInt32(2),
                        StepOrder = r.GetInt32(3),
                        Status = r.GetInt32(4),
                        BatchPriority = r.GetBoolean(5),
                        Assigned_To = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
                        Priority = !r.IsDBNull(7) && r.GetBoolean(7) ? (byte)1 : (byte)0,
                        Tasks_Push = !r.IsDBNull(8) && r.GetBoolean(8),
                        RootId = r.GetInt32(9),
                        ProductToPartId = r.GetInt32(10),
                        IsPainting = !r.IsDBNull(11) && r.GetBoolean(11),
                        CanStart = true,
                        DisplayGroupId = r.GetInt32(9) != r.GetInt32(2)
                            ? r.GetInt32(1)
                            : r.GetInt32(2)
                    });
            }
            await r.DisposeAsync();
            return list;
        }

public async Task<int> GetWorkCenterId(int empId)
{
    return await _db.Employees
        .Where(e => e.Id == empId)
        .Select(e => e.WorkCentrTypeID ?? 0)
        .FirstOrDefaultAsync();
}

public async Task<(bool Success, string? Error)> ValidateTaskExists(
    DbConnection conn,
    DbTransaction tx,
    int taskId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT 1
FROM tasks
WHERE ID = @taskId
  AND IsActive = 1
LIMIT 1;";

    cmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

    var exists = await cmd.ExecuteScalarAsync() != null;

    if (!exists)
        return (false, "Task not found.");

    return (true, null);
}

public async Task<(bool Success, string? Error)> CheckRootStepOrder(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int rootId,
    int currentStepOrder)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
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

    cmd.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));
    cmd.Parameters.Add(new MySqlParameter("@rootId", rootId));
    cmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

    var hasPrev = await cmd.ExecuteScalarAsync() != null;

    if (hasPrev)
        return (false, "Iepriekšējais solis nav pabeigts.");

    return (true, null);
}

public async Task<(bool Success, string? Error)> CheckEmployeeHasActiveTask(DbConnection conn, DbTransaction tx, int empId)
{
    await using var chk = conn.CreateCommand();
    chk.Transaction = tx;

    chk.CommandText = @"
SELECT 1
FROM tasks 
WHERE Claimed_By = @emp 
  AND Tasks_Status = 2 
  AND IsActive = 1
LIMIT 1;";

    var pEmp = chk.CreateParameter();
    pEmp.ParameterName = "@emp";
    pEmp.Value = empId;
    chk.Parameters.Add(pEmp);

    var exists = await chk.ExecuteScalarAsync() != null;

    if (exists)

    return (false, "Jau ir iesākts cits darbs.");

    return (true, null);
}



public async Task<(bool Success, string? Error, int StepOrder)> CheckPriorityAndOrder(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int empId)
{
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

        cur.Parameters.Add(new MySqlParameter("@id", taskId));

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

        checkOrder.Parameters.Add(new MySqlParameter("@taskId", taskId));
        checkOrder.Parameters.Add(new MySqlParameter("@emp", empId));
        checkOrder.Parameters.Add(new MySqlParameter("@curIsPriority", currentIsPriority));
        checkOrder.Parameters.Add(new MySqlParameter("@curBatchOrder", currentBatchOrder));
        checkOrder.Parameters.Add(new MySqlParameter("@curStepOrder", currentStepOrder));

        var higherExists = await checkOrder.ExecuteScalarAsync() != null;

        if (higherExists)
            return (false, "Ir augstākas prioritātes darbs.", currentStepOrder);
    }

    return (true, null, currentStepOrder);
}

public async Task<(bool Success, string? Error, int RootId, bool HasRoot, int StepType)> CheckStepOrder(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int currentStepOrder)
{
    bool hasRoot = false;
    int rootId = 0;
    int stepType = 0;

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
COALESCE(bp.ParentBatchProduct_ID, bp.ID),
ts.Step_Type
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.ID = @taskId;";

        rootCmd.Parameters.Add(new MySqlParameter("@taskId", taskId));

        await using var r = await rootCmd.ExecuteReaderAsync();

        if (await r.ReadAsync())
        {
            hasRoot = r.GetInt32(0) == 1;
            rootId = r.GetInt32(1);
            stepType = r.GetInt32(2);
        }
    }

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
            return (false, "Iepriekšējais solis nav pabeigts.", rootId, hasRoot, stepType);
    }

    return (true, null, rootId, hasRoot, stepType);
}

public async Task<bool> IsDetailPhaseFinishedAll(DbConnection conn, DbTransaction tx, int rootId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT 1
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
AND t.Tasks_Status <> 3
LIMIT 1;";

    cmd.Parameters.Add(new MySqlParameter("@rootId", rootId));

    var hasNotFinished = await cmd.ExecuteScalarAsync() != null;

    return !hasNotFinished;
}

public async Task<(int RootId, bool HasRoot)> GetRootInfo(
    DbConnection conn,
    DbTransaction tx,
    int taskId)
{
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

    return (rootId, hasRoot);
}

public async Task<int> ResolveRootTaskId(DbConnection conn, DbTransaction tx, int taskId)
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

public async Task<(int StepType, int ProductToPartId, int StepOrder, int QtyPerProduct,
    int PlannedQty, int CurrentDone, int BatchProductId, int VersionId, int? RalColorId)>
GetTaskDetails(DbConnection conn, DbTransaction tx, int taskId)
{
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
            throw new Exception("Uzdevuma dati nav atrasti.");

        stepType         = rr.GetInt32(0);
        currentStepOrder = rr.GetInt32(1);
        productToPartId  = rr.GetInt32(2);
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

    return (stepType, productToPartId, currentStepOrder, qtyPerProduct,
            plannedQty, currentDone, batchProductId, versionId, ralColorId);
}

// Finish task un atkarībā no scenārija, update uz 3 (Finished) un atveram nākamos darbus
// iznestie kodu bloki varētu būt atsevišķās funkcijās, lai kods būtu tīrāks
public async Task<(bool Success, string? Error)> ValidateTaskIsInProgress(
    DbConnection conn,
    DbTransaction tx,
    int taskId)
{
    int currentStatus;

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
            return (false, "Uzdevums nav atrasts vai ir neaktīvs.");

        currentStatus = Convert.ToInt32(obj);
    }

    if (currentStatus != 2)
        return (false, "Pabeigt drīkst tikai uzdevumu ar statusu 'Procesā'.");

    return (true, null);
}

public async Task<bool> IsFinalStep(DbConnection conn, DbTransaction tx, int taskId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT ts.IsFinal
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.ID = @id;";

    cmd.Parameters.Add(new MySqlParameter("@id", taskId));

    var obj = await cmd.ExecuteScalarAsync();

    return obj != null && obj != DBNull.Value && Convert.ToBoolean(obj);
}


//iznesam “vai ir nepabeigti assembly taski”
//true = vēl ir nepabeigti
public async Task<bool> HasNotFinishedAssembly(DbConnection conn, DbTransaction tx, int rootId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT 1
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive = 1
AND ts.Step_Type = 2
AND t.Tasks_Status <> 3
AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId
LIMIT 1;";

    cmd.Parameters.Add(new MySqlParameter("@rootId", rootId));

    return await cmd.ExecuteScalarAsync() != null;
}

//iznesam pārbaudi vai jau ir veikts ASSEMBLY stock movement, 
//lai nepieļautu dubultu ASSEMBLY kustību, ja darbs jau ir bijis procesā un ir atvērts no jauna 
//(piem. pēc pārtraukuma)
public async Task<bool> HasAssemblyStockMovement(DbConnection conn, DbTransaction tx, int rootId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT 1
FROM stock_movements sm
JOIN batches_products bp ON bp.ID = sm.BatchProduct_ID
WHERE sm.Move_Type = 'ASSEMBLY'
AND sm.IsActive = 1
AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId
LIMIT 1;";

    cmd.Parameters.Add(new MySqlParameter("@rootId", rootId));

    return await cmd.ExecuteScalarAsync() != null;
}

//iznesam pārbaudi vai STOCK kustība jau eksistē - izmantosim ParentAssembly loģikā
public async Task<bool> HasStockMovement(DbConnection conn, DbTransaction tx, int taskId, int batchProductId, int versionId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT 1
FROM stock_movements
WHERE IsActive = 1
  AND Task_ID = @taskId
  AND BatchProduct_ID = @bpId
  AND Version_ID = @ver
  AND Move_Type = 'STOCK'
LIMIT 1;";

    cmd.Parameters.Add(new MySqlParameter("@taskId", taskId));
    cmd.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
    cmd.Parameters.Add(new MySqlParameter("@ver", versionId));

    return await cmd.ExecuteScalarAsync() != null;
}

//iznesam pārbaudi no Child scenārija - pārbauda vai DETAILED kustība jau eksistē
public async Task<bool> HasDetailedMovement(DbConnection conn, DbTransaction tx, int batchProductId)
{
    await using var cmd = conn.CreateCommand();
    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT 1
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'DETAILED'
  AND IsActive = 1
LIMIT 1;";

    cmd.Parameters.Add(new MySqlParameter("@bpId", batchProductId));

    return await cmd.ExecuteScalarAsync() != null;
}

    }
}