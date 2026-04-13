// Šis kontrolieris ir paredzēts ražošanas prioritāšu pārvaldībai: prioritāšu iestatīšanai, prioritāšu saraksta skatīšanai, kā arī prioritāšu ietekmes analīzei uz ražošanas procesu.

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Controllers;

[ApiController]
[Route("api/production-priorities")]
public class ProductionPrioritiesController : ControllerBase
        {
            private readonly AppDbContext _db;

            public ProductionPrioritiesController(AppDbContext db)
            {
                _db = db;
            }

public class UpdatePriorityRequest
{
    public bool IsPriority { get; set; }
    public int Priority { get; set; }
    public int NormalOrder { get; set; }
}

[HttpGet]
public async Task<IActionResult> Get()
    {
                        var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();

            var list = new List<object>();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
            SELECT
                bp.ID            AS BatchProductId,
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
                b.Batches_Code   AS BatchCode,
                bp.Version_Id    AS VersionId,
                p.Product_Code   AS ProductCode,
                p.Product_Name   AS ProductName,
                c.Category_Name  AS CategoryName,
                v.Version_Name   AS VersionName,
                CASE 
                    WHEN bp.ProductToPart_ID IS NOT NULL
                        AND NOT EXISTS (
                            SELECT 1
                            FROM batches_products bp2
                            WHERE bp2.Batch_Id = bp.Batch_Id
                            AND bp2.Version_Id = bp.Version_Id
                            AND bp2.ProductToPart_ID IS NULL
                            AND bp2.IsActive = 1
                        )
                    THEN tp.TopPart_Name
                    ELSE ''
                END AS TopPartName,
                bp.Planned_Qty   AS Planned,
                bp.is_priority   AS IsPriority,
                bp.Priority      AS Priority,
                bp.NormalOrder   AS NormalOrder,
                CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END AS IsParentProduct,

             -- Detailed Y = cik detaļu šim BatchProduct (no taskiem)
                agg.DetailedY,

            -- Detailed X = cik detaļu ir aktīvas (status 1/2/3) (šim BatchProduct)
            agg.DetailedX,

            -- Detailed Started X = cik detaļu ir iesāktas (status 2/3)
            agg.DetailedStartedX,

                -- Detailed DONE X = cik detaļu pabeigtas (status 3)
            agg.DetailedDoneX,

(
    CASE
        WHEN (
            SELECT COUNT(DISTINCT ts.ProductToPart_ID)
            FROM tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            WHERE t.BatchProduct_ID = bp.ID
              AND t.IsActive = 1
              AND ts.Step_Type = 1
              AND t.Tasks_Status IN (2,3)
        ) > 0
        THEN 1
        ELSE 0
    END
) AS DetailedHasStarted,


    -- Detailed IS DONE = visi Detailed taski šim BatchProduct ir 3
(
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            WHERE t.BatchProduct_ID = bp.ID
              AND t.IsActive = 1
              AND ts.Step_Type = 1
        )
        AND NOT EXISTS (
            SELECT 1
            FROM tasks t
            JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
            WHERE t.BatchProduct_ID = bp.ID
              AND t.IsActive = 1
              AND ts.Step_Type = 1
              AND t.Tasks_Status <> 3
        )
        THEN 1 ELSE 0
    END
) AS DetailedIsDone,

                -- Detailed IN PROGRESS
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status IN (2,3)
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status <> 3
                    )
                    THEN bp.Planned_Qty
                    ELSE 0
                END AS DetailedInProgress,

                -- Detailed FINISH
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status = 3
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 1
                        AND t.Tasks_Status IN (1,2,5)
                    )
                    AND NOT EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 2
                        AND t.Tasks_Status IN (2,3)
                    )
                    THEN bp.Planned_Qty
                    ELSE 0
                END AS DetailedFinish,

                -- Assembly IN PROGRESS
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 2
                        AND t.Tasks_Status IN (2,3)
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM tasks t
                        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                        WHERE t.BatchProduct_ID = bp.ID
                        AND t.IsActive = 1
                        AND ts.Step_Type = 2
                        AND t.Tasks_Status IN (1,2,5)
                    )
                    THEN bp.Planned_Qty
                    ELSE 0
                END AS Assembly,

-- Done (pabeigts gala solis – Step_Type = 3)
(
    SELECT COALESCE(SUM(t.Qty_Done), 0)
    FROM tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    WHERE t.BatchProduct_ID = bp.ID
      AND t.IsActive = 1
      AND t.Tasks_Status = 3
      AND ts.Step_Type = 3
) AS Done,

                -- Finishin X = cik detaļu ir procesā (šim BatchProduct)
-- Finishing STATUS 2 (procesā)
agg.FinishingStatus2,

-- Finishing STATUS 3 (pabeigts)
agg.FinishingStatus3,

-- Finishing STOCK (no Assembly)
(
    SELECT COALESCE(SUM(sm.Stock_Qty), 0)
    FROM stock_movements sm
    WHERE sm.IsActive = 1
      AND sm.BatchProduct_ID = bp.ID
      AND sm.Move_Type = 'ASSEMBLY'
) AS FinishingStock

            FROM batches_products bp
            LEFT JOIN producttopparts ptp ON ptp.ID = bp.ProductToPart_ID
            LEFT JOIN toppart tp ON tp.ID = ptp.TopPart_ID
            LEFT JOIN (
                SELECT
                    t.BatchProduct_ID,

                    COUNT(DISTINCT CASE WHEN ts.Step_Type = 1 THEN ts.ProductToPart_ID END) AS DetailedY,

                    COUNT(DISTINCT CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (1,2,3) THEN ts.ProductToPart_ID END) AS DetailedX,

                    COUNT(DISTINCT CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status IN (2,3) THEN ts.ProductToPart_ID END) AS DetailedStartedX,

                    COUNT(DISTINCT CASE WHEN ts.Step_Type = 1 AND t.Tasks_Status = 3 THEN ts.ProductToPart_ID END) AS DetailedDoneX,

                    SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 2 THEN t.Qty_Done ELSE 0 END) AS FinishingStatus2,

                    SUM(CASE WHEN ts.Step_Type = 3 AND t.Tasks_Status = 3 THEN t.Qty_Done ELSE 0 END) AS FinishingStatus3

                FROM tasks t
                JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                WHERE t.IsActive = 1
                GROUP BY t.BatchProduct_ID
            ) agg ON agg.BatchProduct_ID = bp.ID
            JOIN batches b ON b.ID = bp.Batch_Id
            JOIN versions v   ON v.ID = bp.Version_Id
            JOIN products p   ON p.ID = v.Product_ID
            LEFT JOIN categories c ON c.ID = p.Category_ID
            WHERE
                bp.IsActive = 1
                AND EXISTS (
                    SELECT 1
                    FROM tasks t
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND t.Tasks_Status <> 3
                )
                ORDER BY 
    bp.is_priority DESC,

    CASE 
        WHEN bp.is_priority = 1 THEN 
            CASE WHEN bp.ProductToPart_ID IS NULL THEN 0 ELSE 1 END
    END ASC,

    CASE WHEN bp.is_priority = 1 THEN bp.Priority END ASC,

    CASE 
        WHEN bp.is_priority = 0 THEN 
            CASE WHEN bp.ProductToPart_ID IS NULL THEN 0 ELSE 1 END
    END ASC,

    CASE WHEN bp.is_priority = 0 THEN bp.NormalOrder END ASC;";

                await using var reader = await cmd.ExecuteReaderAsync();

           while (await reader.ReadAsync())
                {
                    list.Add(new
                {
                    BatchProductId      = reader.GetInt32(0),
                    RootId = reader.GetInt32(1),
                    BatchCode = reader.GetString(2),
                    VersionId           = reader.GetInt32(3),
                    ProductCode         = reader.GetString(4),
                    ProductName         = reader.GetString(5),
                    CategoryName        = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    VersionName         = reader.GetString(7),
                    TopPartName = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    Planned             = reader.GetInt32(9),
                    IsPriority          = reader.GetBoolean(10),
                    Priority = Convert.ToInt32(reader.GetValue(11)),
                    NormalOrder = Convert.ToInt32(reader.GetValue(12)),
                    IsParentProduct = reader.GetBoolean(13),
                    DetailedY = reader.GetInt32(14),
                    DetailedX = reader.GetInt32(15),
                    DetailedStartedX = reader.GetInt32(16),
                    DetailedDoneX = reader.GetInt32(17),
                    DetailedHasStarted = reader.GetBoolean(18),
                    DetailedIsDone = reader.GetBoolean(19),

                    DetailedInProgress = reader.GetInt32(20),
                    DetailedFinish = reader.GetInt32(21),
                    Assembly = reader.GetInt32(22),
                    Done = reader.GetInt32(23),
                    FinishingStatus2 = reader.GetInt32(24),
                    FinishingStatus3 = reader.GetInt32(25),
                    FinishingStock = reader.GetInt32(26),
                    });

                }
            }

            return Ok(list);
    }

            [HttpPut("{batchProductId}")]
public async Task<IActionResult> Put(int batchProductId, [FromBody] UpdatePriorityRequest request)
{
    var target = await _db.BatchProducts
        .FirstOrDefaultAsync(x => x.ID == batchProductId);

    if (target == null)
        return NotFound();

    // 🔥 atrodam VISU GRUPU (Root)
var rootId = await _db.BatchProducts
    .Where(x =>
        x.Batch_Id == target.Batch_Id &&
        x.Version_Id == target.Version_Id &&
        x.ProductToPart_ID == null &&
        x.IsActive)
    .Select(x => x.ID)
    .FirstOrDefaultAsync();

var group = await _db.BatchProducts
    .Where(x =>
        x.IsActive &&
        x.Batch_Id == target.Batch_Id &&
        x.Version_Id == target.Version_Id &&
        (
            x.ProductToPart_ID == null ||              // parent
            x.ID == batchProductId                    // child-only
        ))
    .ToListAsync();

    foreach (var bp in group)
    {
        bp.is_priority = request.IsPriority;

        if (request.IsPriority)
        {
            bp.Priority = request.Priority;
            bp.NormalOrder = 0;
        }
        else
        {
            bp.NormalOrder = request.NormalOrder;
            bp.Priority = 0;
        }
    }

    await _db.SaveChangesAsync();

    return NoContent();
}

[HttpGet("list")]
public async Task<IActionResult> GetList()
{
    var rows = await _db.BatchProducts
        .AsNoTracking()
        .Where(bp =>
            bp.IsActive &&
            _db.Tasks.Any(t =>
                t.BatchProduct_ID == bp.ID &&
                t.IsActive &&
                t.Tasks_Status != 3
            )
        )
        .Select(bp => new
        {
            BatchProductId = bp.ID,

            Planned = bp.Planned_Qty,
            Done = bp.Done_Qty,
            IsPriority = bp.is_priority,

            VersionId = bp.Version_Id,

            VersionName = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Select(v => v.VersionName)
                .FirstOrDefault(),

            ProductName = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Join(_db.Products,
                      v => v.ProductId,
                      p => p.Id,
                      (v, p) => p.ProductName)
                .FirstOrDefault(),

            ProductCode = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Join(_db.Products,
                      v => v.ProductId,
                      p => p.Id,
                      (v, p) => p.ProductCode)
                .FirstOrDefault(),

            CategoryName = _db.ProductVersions
                .Where(v => v.Id == bp.Version_Id)
                .Join(_db.Products,
                      v => v.ProductId,
                      p => p.Id,
                      (v, p) => p.CategoryId)
                .Join(_db.Categories,
                      pid => pid,
                      c => c.Id,
                      (pid, c) => c.CategoryName)
                .FirstOrDefault()
        })
        .ToListAsync();

    return Ok(rows);
}

// GET: api/production-priorities/impact
[HttpGet("impact")]
public async Task<IActionResult> GetPriorityImpact()
{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    var result = new List<object>();

    await using (var cmd = conn.CreateCommand())
    {
            cmd.CommandText = @"
WITH task_flags AS (
    SELECT
        t.BatchProduct_ID,
        CASE
    WHEN bp.ProductToPart_ID IS NULL 
         AND EXISTS (
             SELECT 1
             FROM batches_products bp2
             WHERE bp2.Batch_Id = bp.Batch_Id
               AND bp2.Version_Id = bp.Version_Id
               AND bp2.ProductToPart_ID IS NOT NULL
               AND bp2.IsActive = 1
         )
    THEN CONCAT(bp.ID, '_MERGE')

    WHEN bp.ProductToPart_ID IS NOT NULL
         AND EXISTS (
             SELECT 1
             FROM batches_products bp2
             WHERE bp2.Batch_Id = bp.Batch_Id
               AND bp2.Version_Id = bp.Version_Id
               AND bp2.ProductToPart_ID IS NULL
               AND bp2.IsActive = 1
         )
    THEN CONCAT(bp_root.ID, '_MERGE')

    ELSE CAST(bp.ID AS CHAR)
END AS MergeKey,

CASE
    --  Parent + Child → MERGE kā 1 gab
    WHEN bp_root.ID IS NOT NULL
    THEN CONCAT(bp_root.ID, '_', COALESCE(ts.ProductToPart_ID, 0))

    --  Child-only (nav parent) → katrs child savs key
    WHEN bp.ProductToPart_ID IS NOT NULL
    THEN CONCAT(bp.ID, '_', ts.ProductToPart_ID)

    --  Parent-only
    ELSE CONCAT(bp.ID, '_', COALESCE(ts.ProductToPart_ID, 0))
END AS TaskGroupKey,

        COALESCE(ts.ProductToPart_ID, 0) AS ProductToPart_ID,
        MIN(ts.Step_Order) AS Step_Order,
        bp.ProductToPart_ID AS BatchProductToPartId,
        bp.Batch_Id AS BatchId,
        COALESCE(t.Assigned_To, 0) AS Assigned_To,
        wc2.ID AS WorkCentr_ID,
        wc2.WorkCentr_Name AS WorkCenterName,
        wc2.Step_Type_ID AS StepTypeId,
        COALESCE(bp.is_priority,0) AS IsPriority,
        CASE 
            WHEN ts.Step_Type IN (1,2) THEN (bp.Planned_Qty * ptp.Qty_Per_product) * ts.Estimated_Minutes
            WHEN ts.Step_Type = 3 THEN t.Qty_Done * ts.Estimated_Minutes
            ELSE bp.Planned_Qty * ts.Estimated_Minutes
        END AS Estimated_Minutes,
        
CASE
    WHEN NOT EXISTS (
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
END AS CanStart

    FROM tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
    LEFT JOIN batches_products bp_root 
        ON bp_root.Batch_Id = bp.Batch_Id
        AND bp_root.Version_Id = bp.Version_Id
        AND bp_root.ProductToPart_ID IS NULL
        AND bp_root.IsActive = 1
    JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
    LEFT JOIN workcentr_type wc2 ON wc2.ID = ts.WorkCentr_ID

    WHERE 
    t.IsActive = 1
    AND t.Tasks_Status IN (1)

    AND NOT (
        bp.ProductToPart_ID IS NOT NULL
        AND EXISTS (
            SELECT 1
            FROM batches_products bp_parent
            WHERE bp_parent.Batch_Id = bp.Batch_Id
              AND bp_parent.Version_Id = bp.Version_Id
              AND bp_parent.ProductToPart_ID IS NULL
              AND bp_parent.IsActive = 1
        )
    )

GROUP BY
    t.ID,
    t.BatchProduct_ID,
    COALESCE(ts.ProductToPart_ID, 0),
    COALESCE(t.Assigned_To, 0),
    wc2.ID,
    wc2.WorkCentr_Name,
    wc2.Step_Type_ID,
    bp.is_priority
)

SELECT
    wc.WorkCentr_Name AS WorkCenter,
    COALESCE(wc.Step_Type_ID, 99) AS SortStepType,
    emp_list.EmployeeId AS EmployeeId,
    COALESCE(e.Employee_Name, 'Nav piešķirts') AS EmployeeName,

SUM(CASE 
    WHEN tf.IsPriority = 1
    AND tf.Assigned_To IS NOT NULL 
    AND tf.Assigned_To <> 0
    THEN 1 ELSE 0 END) AS PriorityCount,

SUM(CASE 
    WHEN tf.IsPriority = 0
    AND tf.Assigned_To IS NOT NULL 
    AND tf.Assigned_To <> 0
    THEN 1 ELSE 0 END) AS NormalCount,

COUNT(DISTINCT CASE 
    WHEN tf.IsPriority = 1
    AND tf.Assigned_To IS NOT NULL 
    AND tf.Assigned_To <> 0
    AND tf.CanStart = 1
    THEN tf.TaskGroupKey
END) AS PriorityCanStartCount,

COUNT(DISTINCT CASE 
    WHEN tf.IsPriority = 0
    AND tf.Assigned_To IS NOT NULL 
    AND tf.Assigned_To <> 0
    AND tf.CanStart = 1
    THEN tf.TaskGroupKey
END) AS NormalCanStartCount,

COUNT(DISTINCT CASE 
    WHEN tf.CanStart = 1
    AND tf.Assigned_To IS NOT NULL 
    AND tf.Assigned_To <> 0
    THEN tf.TaskGroupKey
END) AS CanStartCount,

SUM(CASE 
    WHEN tf.CanStart = 1
    THEN tf.Estimated_Minutes ELSE 0 END) AS CanStartMinutes,

SUM(CASE 
    WHEN tf.CanStart = 1
    AND tf.Assigned_To IS NOT NULL 
    AND tf.Assigned_To <> 0
    THEN 1 ELSE 0 END) AS AssignedCanStartCount,

SUM(CASE 
    WHEN tf.Assigned_To IS NULL OR tf.Assigned_To = 0
    THEN 1 ELSE 0 END) AS UnassignedTotalCount,

COUNT(DISTINCT CASE 
    WHEN tf.CanStart = 1
    AND (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
    THEN tf.TaskGroupKey
END) AS UnassignedCanStartCount,

-- 🔥 JAUNAIS – pilns sadalījums (NEAIZTIEC esošos laukus)

COUNT(DISTINCT CASE 
    WHEN tf.CanStart = 1
    AND (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
    AND tf.IsPriority = 1
    THEN tf.TaskGroupKey
END) AS UnassignedPriorityCanStartCount,

SUM(
    CASE 
        WHEN tf.CanStart = 0
        AND (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
        AND tf.IsPriority = 1
        THEN 1 ELSE 0 
    END
) AS UnassignedPriorityWaitingCount,

COUNT(DISTINCT CASE 
    WHEN tf.CanStart = 1
    AND (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
        AND tf.IsPriority = 0
        THEN tf.TaskGroupKey
END) AS UnassignedNormalCanStartCount,

SUM(
    CASE 
        WHEN tf.CanStart = 0
        AND (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
        AND tf.IsPriority = 0
        THEN 1 ELSE 0 
    END
) AS UnassignedNormalWaitingCount,

SUM(
    CASE 
        WHEN tf.CanStart = 0
        AND (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
        THEN 1 ELSE 0 
    END
) AS UnassignedWaitingCount,

SUM(CASE 
    WHEN tf.IsPriority = 1
     AND tf.Assigned_To IS NOT NULL 
     AND tf.Assigned_To <> 0
     AND tf.CanStart = 0
    THEN 1 ELSE 0 END) AS PriorityWaitingCount,

SUM(CASE 
    WHEN tf.IsPriority = 0
     AND tf.Assigned_To IS NOT NULL 
     AND tf.Assigned_To <> 0
     AND tf.CanStart = 0
    THEN 1 ELSE 0 END) AS NormalWaitingCount,

SUM(CASE 
    WHEN (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
    AND tf.IsPriority = 1
    THEN 1 ELSE 0 END) AS UnassignedPriorityTotalCount,

SUM(CASE 
    WHEN (tf.Assigned_To IS NULL OR tf.Assigned_To = 0)
    AND tf.IsPriority = 0
    THEN 1 ELSE 0 END) AS UnassignedNormalTotalCount

FROM (
    SELECT ID AS EmployeeId
    FROM employees
    WHERE IsActive = 1

    UNION ALL
    SELECT 0
) emp_list

CROSS JOIN workcentr_type wc

LEFT JOIN task_flags tf 
   ON (
        (emp_list.EmployeeId = 0 AND (tf.Assigned_To IS NULL OR tf.Assigned_To = 0))
        OR
        (emp_list.EmployeeId <> 0 AND tf.Assigned_To = emp_list.EmployeeId)
      )
   AND tf.WorkCentr_ID = wc.ID

LEFT JOIN (
    SELECT ID, Employee_Name
    FROM employees
    WHERE IsActive = 1

    UNION ALL
    SELECT 0 AS ID, 'Nav piešķirts'
) e ON e.ID = emp_list.EmployeeId

GROUP BY
    wc.WorkCentr_Name,
    wc.Step_Type_ID,
    emp_list.EmployeeId

ORDER BY
    SortStepType ASC,
    wc.WorkCentr_Name ASC,
    EmployeeId ASC

            ";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
                {
                    WorkCenter   = reader.GetString(0),
                    SortStepType = reader.IsDBNull(1) ? 99 : reader.GetInt32(1),
                    EmployeeId   = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    EmployeeName = reader.GetString(3),
                    PriorityCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    NormalCount   = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    PriorityCanStartCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    NormalCanStartCount   = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    CanStartCount = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),

                    // 👇 JAUNAIS
                    CanStartMinutes = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),

                    // 👇 VISIEM PĀRĒJIEM +1
                    AssignedCanStartCount = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                    UnassignedTotalCount = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                    UnassignedCanStartCount = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    UnassignedWaitingCount = reader.IsDBNull(17) ? 0 : reader.GetInt32(17),
                    PriorityWaitingCount = reader.IsDBNull(18) ? 0 : reader.GetInt32(18),
                    NormalWaitingCount = reader.IsDBNull(19) ? 0 : reader.GetInt32(19),
                    UnassignedPriorityTotalCount = reader.IsDBNull(20) ? 0 : reader.GetInt32(20),
                    UnassignedNormalTotalCount = reader.IsDBNull(21) ? 0 : reader.GetInt32(21),

                    // 🔥 JAUNIE (pareizās vietās)
                    UnassignedPriorityCanStartCount = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                    UnassignedPriorityWaitingCount = reader.IsDBNull(14) ? 0 : reader.GetInt32(14),
                    UnassignedNormalCanStartCount = reader.IsDBNull(15) ? 0 : reader.GetInt32(15),
                    UnassignedNormalWaitingCount = reader.IsDBNull(16) ? 0 : reader.GetInt32(16),
                });
        }
    }

    return Ok(result);
}

// GET: api/workcenters/list
[HttpGet("/api/workcenters/list")]
public async Task<IActionResult> GetWorkCenters()
{
    var list = await _db.Set<WorkCenter>()
        .AsNoTracking()
        .Where(w => w.IsActive)
        .OrderBy(w => w.WorkCentr_Name)
        .Select(w => w.WorkCentr_Name)
        .ToListAsync();

    return Ok(list);
}

}





