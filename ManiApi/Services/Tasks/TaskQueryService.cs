// Tasks/taskqueryservice.cs -> TaskQueryService klase ar GetForEmployee, GetAllActiveTasks, GetAllActiveTasks


using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
using ManiApi.DTOs.Tasks;
using System.Data;
using MySqlConnector;
using System.Data.Common;
using ManiApi.Services.Tasks;
using ManiApi.Services.Finishing;
using TaskRowDto = ManiApi.Models.TaskRowDto;


namespace ManiApi.Services.Tasks
{
    public class TaskQueryService
    {
        private readonly AppDbContext _db;
        private readonly FinishingTasksService _finishingTasksService;

        public TaskQueryService(
            AppDbContext db,
            FinishingTasksService finishingTasksService)
        {
            _db = db;
            _finishingTasksService = finishingTasksService;
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
        ts.WorkCentr_ID,

        ts.Estimated_Minutes,

        (
            SELECT COALESCE(SUM(s.DurationMinutes),0)
            FROM tasks_work_sessions s
            WHERE s.Task_ID = t.ID
        ) AS ActualMinutes,

        (
            CASE 
                WHEN ts.Step_Type = 1 THEN 
                    bp.Planned_Qty * ts.Estimated_Minutes

                WHEN ts.Step_Type IN (2,3) THEN 
                    t.Qty_Done * ts.Estimated_Minutes
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
            WHEN ts.Step_Type IN (2,3) THEN t.Qty_Done
            ELSE bp.Planned_Qty
        END AS PlannedQty,
        COALESCE(ts.Step_Order, 0) AS StepOrder, -- 11 soļa secība    
        ts.Step_Type              AS StepType,       -- 12 (Detailed/Assembly/Finishing)
        ts.IsFinal,
        ts.IsPainting,
        b.ID                      AS BatchId,       -- 13 (batches.ID)
        bp.Version_Id             AS VersionId,     -- 14 (versions.ID)
        bp.ID                     AS BatchProductId, -- 15  (batches_products.ID)
        COALESCE(bp.ParentBatchProduct_ID, bp.ID) AS RootId, -- 16 RootId
        t.RAL_Color_ID,
        rc.Name

        FROM tasks t
        JOIN batches_products bp   ON bp.ID  = t.BatchProduct_ID AND bp.IsActive = 1
        LEFT JOIN versions v ON v.ID = bp.Version_Id
        JOIN products p   ON p.ID   = v.Product_ID AND p.IsActive = 1
        JOIN batches          b    ON b.ID   = bp.Batch_Id       AND b.IsActive  = 1
        JOIN toppartsteps     ts   ON ts.ID  = t.TopPartStep_ID
        JOIN producttopparts  ptp  ON ptp.ID = ts.ProductToPart_ID
        JOIN toppart          tp   ON tp.ID  = ptp.TopPart_ID
        LEFT JOIN ral_colors rc ON rc.ID = t.RAL_Color_ID
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
                AND t.Tasks_Status IN (1,5)
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
                Console.WriteLine(
    $"RAW TASK -> id={r.GetInt32(0)} status={r.GetInt32(6)} assigned={(r.IsDBNull(22) ? "NULL" : r.GetInt32(22).ToString())}"
);                
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
                    WorkCenterId = r.IsDBNull(16) ? (int?)null : r.GetInt32(16),
                    EstimatedMinutes = r.IsDBNull(17) ? 0 : r.GetInt32(17),
                    ActualMinutes = r.IsDBNull(18) ? 0 : r.GetInt32(18),
                    EstimatedTotalMinutes = r.IsDBNull(19) ? 0 : r.GetInt32(19),
                    EstimatedStartMinutes = r.IsDBNull(20) ? 0 : r.GetInt32(20),
                    BatchCode = r.IsDBNull(21) ? null : r.GetString(21),
                    Done = r.IsDBNull(22) ? 0 : r.GetInt32(22),
                    Assigned_To = r.IsDBNull(23) ? (int?)null : r.GetInt32(23),
                    Planned = r.IsDBNull(24) ? 0 : r.GetInt32(24),
                    StepOrder = r.IsDBNull(25) ? 0 : r.GetInt32(25),
                    StepType = r.IsDBNull(26) ? 0 : r.GetInt32(26),
                    IsFinal = !r.IsDBNull(27) && r.GetBoolean(27),
                    IsPainting = !r.IsDBNull(28) && r.GetBoolean(28),
                    BatchId = r.IsDBNull(29) ? 0 : r.GetInt32(29),
                    VersionId = r.IsDBNull(30) ? 0 : r.GetInt32(30),
                    BatchProductId = r.IsDBNull(31) ? 0 : r.GetInt32(31),
                    RootId = r.IsDBNull(32) ? 0 : r.GetInt32(32),
                    RalColorId = r.IsDBNull(33) ? (int?)null : r.GetInt32(33),
                    RalColorCode = r.IsDBNull(34) ? null : r.GetString(34),
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
            .GroupBy(t => new
                    {
                        t.DisplayGroupId,
                        t.TopPartStepId,

                        // krāsošanā dažādi RAL = dažādi taski
                        RalGroup = workCenterId == 4
                            ? t.RalColorId
                            : null
                    })
            .Select(g =>
        {
            var totalPlanned = g.Sum(x => x.Planned);
            var totalDone = g
                .Where(x => x.Status != 5)
                .Sum(x => x.Done);

            TaskRowDto row;

            // 1. Procesā
            var inProgress = g.FirstOrDefault(x => x.Status == 2);

Console.WriteLine($"GROUP {g.Key.DisplayGroupId} -> inProgress={inProgress?.TaskId} status={inProgress?.Status}");

            if (inProgress != null)
            {
                row = inProgress;
            }
            else
            {
                foreach (var x in g)
                    {
                        Console.WriteLine(
                            $"GROUP ITEM -> task={x.TaskId} assigned={x.Assigned_To} status={x.Status} step={x.TopPartStepId}"
                        );
                    }
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
                        .Where(x => x.Done > 0)
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
            WorkCenterId = row.WorkCenterId,
            RalColorId = row.RalColorId,
            RalColorCode = row.RalColorCode,
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
ORDER BY
    (t2.ID = t.ID) DESC,
    (t2.Tasks_Status = 2) DESC
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


// GET: /api/tasks/detailed-summary-by-batch?batchId=123
public async Task<List<object>> GetDetailedSummaryByBatch(int batchId)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    ts.ProductToPart_ID,
    MIN(CASE 
            WHEN t.Tasks_Status IN (2,3) THEN t.Started_At 
        END) AS StartedAt,
    CASE 
        WHEN SUM(CASE WHEN t.Tasks_Status <> 3 THEN 1 ELSE 0 END) = 0
             AND MAX(t.Finished_At) IS NOT NULL
        THEN MAX(t.Finished_At)
        ELSE NULL
    END AS FinishedAt
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps     ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive      = 1
  AND bp.IsActive     = 1
  AND bp.Batch_Id     = @batch
  AND ts.Step_Type    = 1
GROUP BY ts.ProductToPart_ID;
";

    var pBatch = cmd.CreateParameter();
    pBatch.ParameterName = "@batch";
    pBatch.Value = batchId;
    cmd.Parameters.Add(pBatch);

    var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            ProductToPartId = r.GetInt32(0),
            StartedAt       = r.IsDBNull(1) ? (DateTime?)null : r.GetDateTime(1),
            FinishedAt      = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2)
        });
    }

    return list;
}

public async Task<List<object>> GetDetailedSummaryByBatchProduct(int batchProductId)
{
    if (batchProductId <= 0)
        throw new ArgumentException("batchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
cmd.CommandText = @"
SELECT 
    ts.ProductToPart_ID,
    ts.Step_Type AS StepType,   -- 1 = Detailed, 2 = Assembly, 3 = Finishing

    -- Sākums:
    --  Detailed/Assembly: Step_Order = 10 + status 2/3
    --  Finishing: jebkurš solis ar statusu 2/3
    MIN(
        CASE 
            WHEN ts.Step_Type IN (1,2)
                 AND ts.Step_Order = 10 
                 AND t.Tasks_Status IN (2,3)
            THEN t.Started_At 
            WHEN ts.Step_Type = 3
                 AND t.Tasks_Status IN (2,3)
            THEN t.Started_At
        END
    ) AS StartedAt,

    -- Beigas: IsFinal = 1 šim Step_Type, kad pabeigts (statuss = 3)
    MAX(
        CASE 
            WHEN ts.IsFinal = 1
                 AND t.Tasks_Status = 3
            THEN t.Finished_At
        END
    ) AS FinishedAt

FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps     ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive      = 1
  AND bp.IsActive     = 1
  AND bp.ID           = @bpId          -- KONKRĒTAIS BatchProduct
  AND ts.Step_Type    IN (1,2,3)       -- ← PIEVIENOTS 3 (Finishing)
  AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)  
GROUP BY 
    ts.ProductToPart_ID,
    ts.Step_Type;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bpId";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

    var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            ProductToPartId = r.GetInt32(0),
            StepType        = r.GetInt32(1),  // 1 = Detailed, 2 = Assembly
            StartedAt       = r.IsDBNull(2) ? (DateTime?)null : r.GetDateTime(2),
            FinishedAt      = r.IsDBNull(3) ? (DateTime?)null : r.GetDateTime(3)
        });
    }

    return list;
}

public async Task<List<object>> GetFinishingWaves(int batchProductId, int productToPartId)
{
    if (batchProductId <= 0 || productToPartId <= 0)
        throw new ArgumentException("batchProductId and productToPartId are required.");

    var list = await _db.Tasks
        .Join(_db.TopPartSteps,
            t => t.TopPartStep_ID,
            ts => ts.Id,
            (t, ts) => new { t, ts })
        .GroupJoin(_db.Set<RalColor>(),
            x => x.t.RAL_Color_ID,
            rc => rc.ID,
            (x, rc) => new { x.t, x.ts, rc })
        .SelectMany(
            x => x.rc.DefaultIfEmpty(),
            (x, rc) => new { x.t, x.ts, rc })
            .Where(x =>
                x.t.IsActive &&
                x.t.BatchProduct_ID == batchProductId &&
                x.ts.ProductToPartId == productToPartId &&
                x.ts.StepType == 3
                    
)

        .OrderByDescending(x => x.t.ID)
        .Select(x => new
            {
                TaskId = x.t.ID,
                Status = x.t.Tasks_Status,
                RalColorId = x.t.RAL_Color_ID,
                Qty = x.t.Qty_Done,
                Assigned_To = x.t.Assigned_To,
                Claimed_By = x.t.Claimed_By,
                StartedAt = x.t.Started_At,
                FinishedAt = x.t.Finished_At,
                Comment = x.t.Tasks_Comment,
                RalName = x.rc != null ? x.rc.Name : null
            })

        .ToListAsync();

Console.WriteLine("[finishing-waves] " + string.Join(" | ", list.Select(x => $"{x.TaskId}:{x.RalName}:{(x.Comment ?? "NULL")}")));

    return list.Cast<object>().ToList();
}

public async Task<List<object>> GetFinishingWavesChild(
    int batchProductId,
    int productToPartId)
{
    if (batchProductId <= 0 || productToPartId <= 0)
        throw new ArgumentException("batchProductId and productToPartId are required.");

    var list = await _db.Tasks
        .Join(_db.TopPartSteps,
            t => t.TopPartStep_ID,
            ts => ts.Id,
            (t, ts) => new { t, ts })

        .GroupJoin(_db.Set<RalColor>(),
            x => x.t.RAL_Color_ID,
            rc => rc.ID,
            (x, rc) => new { x.t, x.ts, rc })

        .SelectMany(
            x => x.rc.DefaultIfEmpty(),
            (x, rc) => new { x.t, x.ts, rc })

        .Where(x =>
            x.t.IsActive &&
            x.t.BatchProduct_ID == batchProductId &&
            x.ts.ProductToPartId == productToPartId &&
            x.ts.IsPainting
        )

        .OrderByDescending(x => x.t.ID)

        .Select(x => new
        {
            TaskId = x.t.ID,
            Status = x.t.Tasks_Status,
            Qty = x.t.Qty_Done,
            Assigned_To = x.t.Assigned_To,
            Claimed_By = x.t.Claimed_By,
            StartedAt = x.t.Started_At,
            FinishedAt = x.t.Finished_At,
            Comment = x.t.Tasks_Comment,
            RalName = x.rc != null ? x.rc.Name : null
        })

        .ToListAsync();

    return list.Cast<object>().ToList();
}

public async Task<int> GetFinishingInProgressByVersion(int versionId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT COALESCE(SUM(t.Qty_Done), 0) AS FinishingInProgress
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID AND ts.IsActive = 1
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID AND bp.IsActive = 1
WHERE bp.Version_Id = @vid
  AND t.IsActive = 1
  AND ts.Step_Type = 3
  AND t.Tasks_Status = 2;";

    cmd.Parameters.Add(new MySqlParameter("@vid", versionId));

    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}

public async Task<int> GetFinishingAllocatedByVersion(int versionId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT COALESCE(SUM(t.Qty_Done), 0) AS FinishingAllocated
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID AND ts.IsActive = 1
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID AND ptp.IsActive = 1
WHERE ptp.Version_ID = @vid
  AND t.IsActive = 1
  AND ts.Step_Type = 3
  AND t.Tasks_Status IN (1,2);";

    cmd.Parameters.Add(new MySqlParameter("@vid", versionId));

    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}

public async Task<List<object>> GetDetailedIndicators(int batchProductId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    ts.ProductToPart_ID,

    SUM(CASE WHEN t.Tasks_Status = 1 THEN 1 ELSE 0 END) AS Cnt1,
    SUM(CASE WHEN t.Tasks_Status = 2 THEN 1 ELSE 0 END) AS Cnt2,
    SUM(CASE WHEN t.Tasks_Status = 3 THEN 1 ELSE 0 END) AS Cnt3,
    SUM(CASE WHEN t.Tasks_Status = 5 THEN 1 ELSE 0 END) AS Cnt5,
    COUNT(*) AS TotalCnt

FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID

WHERE t.IsActive = 1
  AND ts.Step_Type = 1
  AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)
  AND t.BatchProduct_ID IN (
      SELECT bp2.ID
      FROM batches_products bp2
      WHERE bp2.IsActive = 1
        AND bp2.Batch_Id = (
            SELECT bp0.Batch_Id
            FROM batches_products bp0
            WHERE bp0.ID = @bp
            LIMIT 1
        )
        AND bp2.Version_Id = (
            SELECT bp0.Version_Id
            FROM batches_products bp0
            WHERE bp0.ID = @bp
            LIMIT 1
        )
  )

GROUP BY ts.ProductToPart_ID;
";

    cmd.Parameters.Add(new MySqlParameter("@bp", batchProductId));

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        int cnt1 = r.GetInt32(1);
        int cnt2 = r.GetInt32(2);
        int cnt3 = r.GetInt32(3);
        int cnt5 = r.GetInt32(4);
        int total = r.GetInt32(5);

        string state =
            cnt5 == total ? "gray" :
            cnt3 == total ? "green" :
            cnt2 > 0      ? "yellow" :
            cnt1 == total ? "blue" :
                            "gray";

        list.Add(new
        {
            ProductToPartId = r.GetInt32(0),
            State = state
        });
    }

    return list;
}

public async Task<List<object>> GetAssemblyIndicators(int batchProductId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    ts.ProductToPart_ID,

    SUM(CASE WHEN t.Tasks_Status = 1 THEN 1 ELSE 0 END) AS Cnt1,
    SUM(CASE WHEN t.Tasks_Status = 2 THEN 1 ELSE 0 END) AS Cnt2,
    SUM(CASE WHEN t.Tasks_Status = 3 THEN 1 ELSE 0 END) AS Cnt3,
    SUM(CASE WHEN t.Tasks_Status = 5 THEN 1 ELSE 0 END) AS Cnt5,
    COUNT(*) AS TotalCnt

FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID

WHERE t.IsActive = 1
  AND t.BatchProduct_ID = @bp
  AND ts.Step_Type = 2

GROUP BY ts.ProductToPart_ID;
";

    cmd.Parameters.Add(new MySqlParameter("@bp", batchProductId));

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        int cnt1 = r.GetInt32(1);
        int cnt2 = r.GetInt32(2);
        int cnt3 = r.GetInt32(3);
        int cnt5 = r.GetInt32(4);
        int total = r.GetInt32(5);

        string state =
            cnt5 == total ? "gray" :
            cnt3 == total ? "green" :
            cnt2 > 0      ? "yellow" :
            cnt1 == total ? "blue" :
                            "gray";

        list.Add(new
        {
            ProductToPartId = r.GetInt32(0),
            State = state
        });
    }

    return list;
}

public async Task<List<object>> GetFinishingIndicators(int batchProductId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    ts.ProductToPart_ID,

    SUM(CASE WHEN t.Tasks_Status = 1 THEN 1 ELSE 0 END) AS Cnt1,
    SUM(CASE WHEN t.Tasks_Status = 2 THEN 1 ELSE 0 END) AS Cnt2,
    SUM(CASE WHEN t.Tasks_Status = 3 THEN 1 ELSE 0 END) AS Cnt3,
    COUNT(*) AS TotalCnt

FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive = 1
  AND t.BatchProduct_ID = @bp

AND (
    (
        EXISTS (
            SELECT 1
            FROM batches_products bp0
            WHERE bp0.ID = @bp
              AND bp0.ProductToPart_ID IS NULL
        )
    )

    OR

    (
        t.BatchProduct_ID = @bp
    )
)

  AND (
            ts.Step_Type = 3
            OR
            (
                ts.Step_Type = 1
                AND ts.IsPainting = 1
            )
        )
GROUP BY ts.ProductToPart_ID;
";

    cmd.Parameters.Add(new MySqlParameter("@bp", batchProductId));

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        int cnt1 = r.GetInt32(1);
        int cnt2 = r.GetInt32(2);
        int cnt3 = r.GetInt32(3);
        int total = r.GetInt32(4);

        string state =
            total == 0
                ? "gray"
                : cnt3 == total
                    ? "green"
                    : (cnt1 > 0 || cnt2 > 0)
                        ? "yellow"
                        : "gray";

        list.Add(new
        {
            ProductToPartId = r.GetInt32(0),
            State = state
        });
    }

    await r.DisposeAsync();

    var childParts = await _db.BatchProducts
            .Where(x =>
                x.IsActive &&
                x.ID == batchProductId &&
                x.ProductToPart_ID != null)
            .Select(x => x.ProductToPart_ID!.Value)
            .ToListAsync();

        foreach (var ptpId in childParts)
        {
            var exists = list.Any(x =>
                (int)x.GetType().GetProperty("ProductToPartId")!.GetValue(x)! == ptpId);

            if (exists)
                continue;

            var child = await _finishingTasksService
                .GetChildFinishingData(batchProductId, ptpId);

Console.WriteLine($"CHILD TEST -> bp={batchProductId} ptp={ptpId} paint={child.isPainting} avail={child.availableQty}");

            if (!child.isPainting || child.availableQty <= 0)
                continue;

            list.Add(new
            {
                ProductToPartId = ptpId,
                State = "blue"
            });
        }

    return list;
}

public async Task<List<object>> GetByStep(int batchProductId, int topPartStepId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    t.ID            AS TaskId,
    t.Tasks_Status  AS Status,
    t.Assigned_To   AS AssignedTo,
    COALESCE(t.Qty_Done, 0) AS Done
FROM tasks t
WHERE t.IsActive = 1
  AND t.BatchProduct_ID = @bp
  AND t.TopPartStep_ID  = @step
ORDER BY t.ID;
";

    cmd.Parameters.Add(new MySqlParameter("@bp", batchProductId));
    cmd.Parameters.Add(new MySqlParameter("@step", topPartStepId));

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        list.Add(new
        {
            TaskId      = r.GetInt32(0),
            Status      = r.GetInt32(1),
            Assigned_To = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            Done        = r.GetInt32(3)
        });
    }

    return list;
}

public async Task<List<object>> GetTasksByBatch(int batchProductId, int stepType)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT
    t.ID               AS TaskId,
    t.Tasks_Status     AS Status,
    t.Assigned_To,
    t.Claimed_By,
    COALESCE(t.Qty_Done, 0) AS Done,
    ts.ID AS TopPartStepId,
    COALESCE(ptpParent.ID, ptp.ID) AS ProductToPartId,
    tp.ID              AS TopPartId,
    ptp.TopPart_ID     AS TopPartIdRaw,
    t.Started_At,
    t.Finished_At,
    t.Tasks_Comment AS Comment,
    t.Is_Comment_For_Employee AS IsCommentForEmployee,
    tp.TopPart_Name AS PartName,
    rc.Name AS RalName,
    bp.ParentBatchProduct_ID,
    bp.ProductToPart_ID,
    bp.Planned_Qty AS BatchPlannedQty,
    ptp.Qty_Per_product AS QtyPerProduct

FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID

JOIN toppartsteps    ts  ON ts.ID = t.TopPartStep_ID

JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart         tp  ON tp.ID  = ptp.TopPart_ID

LEFT JOIN producttopparts ptpParent
    ON ptpParent.Version_ID = bp.Version_Id
    AND ptpParent.TopPart_ID = ptp.TopPart_ID
    AND ptpParent.IsActive = 1

LEFT JOIN ral_colors rc ON rc.ID = t.RAL_Color_ID
WHERE t.IsActive = 1
  AND ts.Step_Type = @stepType
  AND t.BatchProduct_ID IN (
      SELECT bp2.ID
      FROM batches_products bp2
      WHERE bp2.IsActive = 1
        AND bp2.Batch_Id = (
            SELECT bp0.Batch_Id
            FROM batches_products bp0
            WHERE bp0.ID = @bpId
            LIMIT 1
        )
        AND bp2.Version_Id = (
            SELECT bp0.Version_Id
            FROM batches_products bp0
            WHERE bp0.ID = @bpId
            LIMIT 1
        )
  )
ORDER BY ts.Step_Order, t.ID;
";
    cmd.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
    cmd.Parameters.Add(new MySqlParameter("@stepType", stepType));

    var list = new List<object>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
            list.Add(new
            {
                TaskId        = r.GetInt32(0),
                Status        = r.GetInt32(1),
                Assigned_To   = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                Claimed_By    = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
                Done          = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                TopPartStepId = r.GetInt32(5),
                ProductToPartId = r.GetInt32(6),
                TopPartId     = r.GetInt32(7),
                TopPartIdRaw  = r.GetInt32(8),
                StartedAt     = r.IsDBNull(9) ? (DateTime?)null : r.GetDateTime(9),
                FinishedAt    = r.IsDBNull(10) ? (DateTime?)null : r.GetDateTime(10),
                Comment = r.IsDBNull(11) ? null : r.GetString(11),
                IsCommentForEmployee = !r.IsDBNull(12) && r.GetBoolean(12),
                PartName = r.IsDBNull(13) ? null : r.GetString(13),
                RalName = r.IsDBNull(14) ? null : r.GetString(14),
                ParentBatchProductId = r.IsDBNull(15) ? (int?)null : r.GetInt32(15),
                ProductToPartId_BP   = r.IsDBNull(16) ? (int?)null : r.GetInt32(16),   
                BatchPlannedQty = r.IsDBNull(17) ? 0 : r.GetInt32(17),
                QtyPerProduct   = r.IsDBNull(18) ? 0 : r.GetInt32(18),  
            });

    }

    return list;
}

public async Task<List<string>> GetWorkCenters()
{
    return await _db.WorkCenters
        .Where(x => x.IsActive)
        .OrderBy(x => x.WorkCenter_Order)
        .Select(x => x.WorkCentr_Name)
        .ToListAsync();
}

public async Task<int> GetAssemblyAvailableUi(int batchProductId)
{
    var assemblyStock = await _db.StockMovements
        .Where(x =>
            x.IsActive &&
            x.BatchProduct_ID == batchProductId &&
            x.Move_Type == MoveType.ASSEMBLY)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    return Math.Max(assemblyStock, 0);
}

public async Task<(string? EmployeeName, string? WorkCenterName, int? WorkCentrTypeID)> GetEmployeeHeader(int empId)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    string? employeeName = null;
    string? workCenterName = null;
    int? employeeWorkCenterId = null;

    await using (var cmdHeader = conn.CreateCommand())
    {
        cmdHeader.CommandText = @"
SELECT 
    e.Employee_Name,
    e.WorkCentrTypeID,
    wc.Workcentr_Name
FROM employees e
LEFT JOIN workcentr_type wc ON wc.ID = e.WorkCentrTypeID
WHERE e.ID = @empId;";

        cmdHeader.Parameters.Add(new MySqlParameter("@empId", empId));

        await using var rHeader = await cmdHeader.ExecuteReaderAsync();

        if (await rHeader.ReadAsync())
        {
            employeeName = rHeader.IsDBNull(0) ? null : rHeader.GetString(0);
            employeeWorkCenterId = rHeader.IsDBNull(1) ? (int?)null : rHeader.GetInt32(1);
            workCenterName = rHeader.IsDBNull(2) ? null : rHeader.GetString(2);
        }
        
    }

    return (employeeName, workCenterName, employeeWorkCenterId);
}

public async Task<List<TaskItemDto>> GetEmployeeInProgress(int empId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    wc.Workcentr_Name AS WorkCenter,
    ts.WorkCentr_ID AS WorkCentrTypeID,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName, 
    " + GetQtySql() + @" AS Qty,
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
WHERE t.IsActive = 1
  AND t.Tasks_Status = 2
  AND t.Claimed_By = @empId
ORDER BY
  bp.is_priority DESC,
  bp.Priority ASC,
  t.Tasks_Priority DESC,
  ts.Step_Order ASC;
";

    cmd.Parameters.Add(new MySqlParameter("@empId", empId));

    return await ExecuteTaskQuery(conn, cmd.CommandText,
        new MySqlParameter("@empId", empId));
}

public async Task<List<TaskItemDto>> GetEmployeePriority(int empId, int? workCenterId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    wc.Workcentr_Name AS WorkCenter,
    ts.WorkCentr_ID AS WorkCentrTypeID,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
    " + GetQtySql() + @" AS Qty,
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
  bp.Priority ASC,
  ts.Step_Order ASC;
";

    cmd.Parameters.Add(new MySqlParameter("@empId", empId));
    cmd.Parameters.Add(new MySqlParameter("@wc", (object?)workCenterId ?? DBNull.Value));

    var list = await ExecuteTaskQuery(conn, cmd.CommandText,
    new MySqlParameter("@empId", empId),
    new MySqlParameter("@wc", (object?)workCenterId ?? DBNull.Value));

list.ForEach(x => x.BatchPriority = true);

return list;
}

public async Task<List<TaskItemDto>> GetEmployeeNormal(int empId, int? workCenterId)
{
    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }
    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
SELECT
    wc.Workcentr_Name AS WorkCenter,
    ts.WorkCentr_ID AS WorkCentrTypeID,
    wc.ID AS WorkCenterSort,
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
    " + GetQtySql() + @" AS Qty,

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
  bp.Priority ASC,
  bp.NormalOrder ASC,
  ts.Step_Order ASC;
";

    cmd.Parameters.Add(new MySqlParameter("@empId", empId));
    cmd.Parameters.Add(new MySqlParameter("@wc", (object?)workCenterId ?? DBNull.Value));

    return await ExecuteTaskQuery(conn, cmd.CommandText,
    new MySqlParameter("@empId", empId),
    new MySqlParameter("@wc", (object?)workCenterId ?? DBNull.Value));
}

private string GetQtySql()
{
    return @"
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
END";
}

private async Task<List<TaskItemDto>> ExecuteTaskQuery(
    DbConnection conn,
    string sql,
    params MySqlParameter[] parameters)
{
    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;

    if (parameters != null && parameters.Length > 0)
        cmd.Parameters.AddRange(parameters);

    var list = new List<TaskItemDto>(100);

    await using var r = await cmd.ExecuteReaderAsync();

    while (await r.ReadAsync())
    {
        list.Add(new TaskItemDto
        {
            BatchPriority = false, // default, override ja vajag
            WorkCenter = r.IsDBNull(0) ? null : r.GetString(0),
            WorkCenterTypeId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1),
            WorkCenterSort = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
            TaskId = r.GetInt32(3),
            BatchProductId = r.GetInt32(4),
            BatchCode = r.GetString(5),
            ProductName = r.GetString(6),
            Qty = r.IsDBNull(7) ? 0 : Convert.ToInt32(r.GetValue(7)),
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

    return list;
}

public async Task<List<UnassignedTaskV2Dto>> GetUnassignedTasksV2()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    // PAGAIDĀM atstāj tukšu
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
  AND t.Assigned_To IS NULL;";

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

return result;

}

public async Task<List<UnassignedTaskDto>> GetUnassignedTasks()
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
  ts.Step_Order;";

    var list = new List<UnassignedTaskDto>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new UnassignedTaskDto
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


    var raw = list;

var groups = raw.GroupBy(x => new 
{ 
    RootId = (int)x.RootId,
    StepOrder = (int)x.StepOrder
});

var result = new List<UnassignedTaskDto>();

foreach (var g in groups)
{
    var items = g.ToList();

var hasParent = items.Any(x => x.BatchProductId == x.RootId);
var hasChild = items.Any(x => x.BatchProductId != x.RootId);

    if (hasParent && hasChild)
    {
        var first = items.First();

        var parentQty = items
            .Where(x => x.BatchProductId == x.RootId)
            .Sum(x => x.Qty);

        var childQty = items
            .Where(x => x.BatchProductId != x.RootId)
            .Sum(x => x.Qty);

            var totalQty = parentQty + childQty;

            result.Add(new UnassignedTaskDto
                    {
                        WorkCenter = first.WorkCenter,
                        WorkCenterSort = first.WorkCenterSort,
                        TaskId = first.TaskId,
                        BatchProductId = first.BatchProductId,
                        ProductToPartId = first.ProductToPartId,
                        BatchCode = first.BatchCode,
                        ProductName = first.ProductName,
                        TopPartName = first.TopPartName,
                        StepName = first.StepName,
                        TopPartStepId = first.TopPartStepId,
                        Qty = totalQty,
                        QtyBreakdown = $"{parentQty}+{childQty}",
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
                }
    else
{
    foreach (var t in items)
    {
        var isParent = hasParent
            ? t.BatchProductId == t.RootId
            : true;

        result.Add(new UnassignedTaskDto
        {
            WorkCenter = t.WorkCenter,
            WorkCenterSort = t.WorkCenterSort,
            TaskId = t.TaskId,
            BatchProductId = t.BatchProductId,
            ProductToPartId = t.ProductToPartId,
            BatchCode = t.BatchCode,
            ProductName = t.ProductName,
            TopPartName = t.TopPartName,
            StepName = t.StepName,
            TopPartStepId = t.TopPartStepId,
            Qty = t.Qty,
            EstimatedMinutes = t.EstimatedMinutes,
            Status = t.Status,
            CanStart = t.CanStart,
            Assigned_To = t.Assigned_To,
            BatchPriority = t.BatchPriority,
            Tasks_Priority = t.Tasks_Priority,
            Tasks_Push = t.Tasks_Push,
            StepOrder = t.StepOrder,
            StepType = t.StepType,

            RowType = isParent ? "Parent" : "ChildOnly"
        });
    }
}
}

return result;

}

public async Task<List<ReadyDetailPartDto>> GetReadyDetailParts(int batchProductId)
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    var result = new List<ReadyDetailPartDto>();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT 
    ptp.ID AS ProductToPartId,
    tp.TopPart_Name,
    SUM(bp.Planned_Qty) AS Qty,
    CASE 
        WHEN SUM(CASE WHEN t.Tasks_Status <> 3 THEN 1 ELSE 0 END) = 0 
        THEN 'done'
        ELSE 'notdone'
    END AS State
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID

WHERE t.IsActive = 1
    AND ts.Step_Type = 1
    AND t.BatchProduct_ID IN (
    SELECT bp2.ID
    FROM batches_products bp2
    WHERE bp2.IsActive = 1
            AND bp2.Batch_Id = (
                SELECT Batch_Id FROM batches_products WHERE ID = @bpId
            )
            AND bp2.Version_Id = (
                SELECT Version_Id FROM batches_products WHERE ID = @bpId
            )
        )
    AND bp.ProductToPart_ID IS NOT NULL

GROUP BY ptp.ID, tp.TopPart_Name

HAVING 
    SUM(CASE WHEN t.Tasks_Status <> 3 THEN 1 ELSE 0 END) = 0
";

    cmd.Parameters.Add(new MySqlParameter("@bpId", batchProductId));

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        result.Add(new ReadyDetailPartDto
        {
            ProductToPartId = r.GetInt32(0),
            Name = r.GetString(1),
            Qty = r.GetInt32(2),
            State = r.GetString(3)          
        });
    }

    return result;
}

public async Task<bool> HasPlannedMovement(
    DbConnection conn,
    DbTransaction tx,
    int batchProductId)
{
    await using var cmd = conn.CreateCommand();

    cmd.Transaction = tx;

    cmd.CommandText = @"
SELECT COALESCE(SUM(sm.Stock_Qty), 0)
FROM stock_movements sm
WHERE sm.BatchProduct_ID = @bpId
  AND sm.Move_Type = 'PLANNED'
  AND sm.IsActive = 1;";

    cmd.Parameters.Add(
        new MySqlParameter("@bpId", batchProductId)
    );

    var result = Convert.ToInt32(
        await cmd.ExecuteScalarAsync()
    );

    return result > 0;
}

    }
}