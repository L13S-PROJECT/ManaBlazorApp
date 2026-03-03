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
                b.Batches_Code   AS BatchCode,
                bp.Version_Id    AS VersionId,
                p.Product_Code   AS ProductCode,
                p.Product_Name   AS ProductName,
                c.Category_Name  AS CategoryName,
                v.Version_Name   AS VersionName,
                bp.Planned_Qty   AS Planned,
                bp.is_priority   AS IsPriority,
                bp.Priority      AS Priority,

                -- Detailed Y = cik detaļu šim BatchProduct (no taskiem)
                (
                    SELECT COUNT(DISTINCT ts.ProductToPart_ID)
                    FROM tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND ts.Step_Type = 1
                ) AS DetailedY,


            -- Detailed X = cik detaļu ir aktīvas (status 1/2/3) (šim BatchProduct)
            (
                SELECT COUNT(DISTINCT ts.ProductToPart_ID)
                FROM tasks t
                JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                WHERE t.BatchProduct_ID = bp.ID
                AND t.IsActive = 1
                AND ts.Step_Type = 1
                AND t.Tasks_Status IN (1,2,3)
            ) AS DetailedX,

            -- Detailed Started X = cik detaļu ir iesāktas (status 2/3)
                (
                    SELECT COUNT(DISTINCT ts.ProductToPart_ID)
                    FROM tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND ts.Step_Type = 1
                    AND t.Tasks_Status IN (2,3)
                ) AS DetailedStartedX,

                -- Detailed DONE X = cik detaļu pabeigtas (status 3)
                (
                    SELECT COUNT(DISTINCT ts.ProductToPart_ID)
                    FROM tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    WHERE t.BatchProduct_ID = bp.ID
                    AND t.IsActive = 1
                    AND ts.Step_Type = 1
                    AND t.Tasks_Status = 3
                ) AS DetailedDoneX,

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
(
    SELECT COALESCE(SUM(t.Qty_Done), 0)
    FROM tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    WHERE t.BatchProduct_ID = bp.ID
      AND t.IsActive = 1
      AND ts.Step_Type = 3
      AND t.Tasks_Status = 2
) AS FinishingStatus2,

-- Finishing STATUS 3 (pabeigts)
(
    SELECT COALESCE(SUM(t.Qty_Done), 0)
    FROM tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    WHERE t.BatchProduct_ID = bp.ID
      AND t.IsActive = 1
      AND ts.Step_Type = 3
      AND t.Tasks_Status = 3
) AS FinishingStatus3,

-- Finishing STOCK (no Assembly)
(
    SELECT COALESCE(SUM(sm.Stock_Qty), 0)
    FROM stock_movements sm
    WHERE sm.IsActive = 1
      AND sm.BatchProduct_ID = bp.ID
      AND sm.Move_Type = 'ASSEMBLY'
) AS FinishingStock,

-- Finishing STATUS 1 (rezervēts, nav sācies)
(
    SELECT COALESCE(SUM(t.Qty_Done), 0)
    FROM tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    WHERE t.BatchProduct_ID = bp.ID
      AND t.IsActive = 1
      AND ts.Step_Type = 3
      AND t.Tasks_Status = 1
      AND COALESCE(t.Qty_Done, 0) > 0
) AS FinishingStatus1
            FROM batches_products bp
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
                ORDER BY bp.is_priority DESC, bp.Priority ASC;";

                await using var reader = await cmd.ExecuteReaderAsync();

           while (await reader.ReadAsync())
                {
                    list.Add(new
                {
                    BatchProductId      = reader.GetInt32(0),
                    BatchCode = reader.GetString(1),
                    VersionId           = reader.GetInt32(2),
                    ProductCode         = reader.GetString(3),
                    ProductName         = reader.GetString(4),
                    CategoryName        = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    VersionName         = reader.GetString(6),
                    Planned             = reader.GetInt32(7),
                    IsPriority          = reader.GetBoolean(8),
                    Priority = Convert.ToInt32(reader.GetValue(9)),
                    DetailedY = reader.GetInt32(10),
                    DetailedX = reader.GetInt32(11),
                    DetailedStartedX    = reader.GetInt32(12),
                    DetailedDoneX       = reader.GetInt32(13),
                    DetailedHasStarted  = reader.GetBoolean(14),
                    DetailedIsDone      = reader.GetBoolean(15),

                    DetailedInProgress  = reader.GetInt32(16),
                    DetailedFinish      = reader.GetInt32(17),
                    Assembly            = reader.GetInt32(18),
                    Done                = reader.GetInt32(19),
                    FinishingStatus2 = reader.GetInt32(20),
                    FinishingStatus3 = reader.GetInt32(21),
                    FinishingStock   = reader.GetInt32(22),
                    FinishingStatus1 = reader.GetInt32(23),

                });

                }
            }

            return Ok(list);
    }

            [HttpPut("{batchProductId}")]
        public async Task<IActionResult> Put(int batchProductId, [FromBody] UpdatePriorityRequest request)
        {
            var bp = await _db.BatchProducts
                .FirstOrDefaultAsync(x => x.ID == batchProductId);

            if (bp == null)
                return NotFound();

            bp.is_priority = request.IsPriority;
            bp.Priority = request.Priority;

Console.WriteLine($"UPDATE: {batchProductId} -> {request.Priority}");

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
SELECT
    wc.WorkCentr_Name AS WorkCenter,
    COALESCE(wc.Step_Type_ID, 99) AS SortStepType,
    e.ID AS EmployeeId,
    e.Employee_Name AS EmployeeName,

    SUM(CASE WHEN bp.is_priority = 1 AND t.ID IS NOT NULL THEN 1 ELSE 0 END) AS PriorityCount,
    SUM(CASE WHEN bp.is_priority = 0 AND t.ID IS NOT NULL THEN 1 ELSE 0 END) AS NormalCount

FROM workcentr_type wc

CROSS JOIN (
    SELECT ID, Employee_Name
    FROM employees
    WHERE IsActive = 1

    UNION ALL
    SELECT 0 AS ID, 'Nav piešķirts' AS Employee_Name
) e

LEFT JOIN toppartsteps s
    ON s.WorkCentr_ID = wc.ID
    AND s.IsActive = 1

LEFT JOIN tasks t
    ON t.TopPartStep_ID = s.ID
    AND t.IsActive = 1
    AND t.Tasks_Status IN (1,2)
    AND IFNULL(t.Assigned_To, 0) = e.ID

LEFT JOIN batches_products bp
    ON bp.ID = t.BatchProduct_ID
    AND bp.IsActive = 1

WHERE wc.IsActive = 1

GROUP BY
    wc.WorkCentr_Name,
    wc.Step_Type_ID,
    e.ID,
    e.Employee_Name

ORDER BY
    SortStepType ASC,
    wc.WorkCentr_Name ASC,
    EmployeeName ASC;

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
                    NormalCount   = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
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





