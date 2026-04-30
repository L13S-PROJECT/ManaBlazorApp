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

    }
}