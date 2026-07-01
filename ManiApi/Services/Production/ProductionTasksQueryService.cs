/*ProductionTasksQueryService.cs*/

using ManiApi.Data;
using System.Data;
using ManiApi.DTOs.Production;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace ManiApi.Services.Production;

public sealed class ProductionTasksQueryService
{
    private readonly AppDbContext _db;

    public ProductionTasksQueryService(AppDbContext db)
    {
        _db = db;
    }

public async Task<List<ProductionBatchRowDto>> GetProductionBatchesRowsV2()
{
    var conn = _db.Database.GetDbConnection();
await conn.OpenAsync();

await using var cmd = conn.CreateCommand();

var sql = @"
SELECT
    b.ID AS BatchId,
    b.Batches_Code AS BatchCode,    
    bp.RootId,

    MIN(bp.ID) AS BatchProductId,

    bp.Version_Id AS VersionId,
    p.Product_Name AS ProductName,
    p.Product_Code AS ProductCode,
    c.Category_Name AS CategoryName,
    bp.ProductToPart_ID AS ProductToPartId,
    p.Category_ID AS CategoryId,
    c.Parent_ID AS ParentCategoryId,
    v.Version_Name AS VersionName,
    v.IsActive AS VersionIsActive,
    v.Production_Model AS ProductionModel,

    CASE
    WHEN bp.ProductToPart_ID IS NULL
    THEN
        CASE
            WHEN COALESCE(ch.ChildCount,0) > 0
            THEN CONCAT('+', ch.ChildCount)
            ELSE '-'
        END
    ELSE (
        SELECT tp.TopPart_Name
        FROM producttopparts ptp
        JOIN toppart tp ON tp.ID = ptp.TopPart_ID
        WHERE ptp.ID = bp.ProductToPart_ID
        LIMIT 1
    )
END AS DetailName,

CASE
    WHEN bp.ProductToPart_ID IS NULL
    THEN (
        SELECT GROUP_CONCAT(DISTINCT tp.TopPart_Name SEPARATOR ' | ')
        FROM batches_products bp2
        JOIN producttopparts ptp 
            ON ptp.ID = bp2.ProductToPart_ID
        JOIN toppart tp 
            ON tp.ID = ptp.TopPart_ID
        WHERE bp2.Batch_Id = bp.Batch_Id
          AND bp2.Version_Id = bp.Version_Id
          AND bp2.ProductToPart_ID IS NOT NULL
          AND bp2.IsActive = 1
          AND tp.Stage = 1
          AND tp.IsActive = 1
    )
    ELSE NULL
END AS HiddenDetailNames,

    MAX(bp.is_priority) AS IsPriority,
    MAX(CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END) AS HasParent,

    CASE
        WHEN bp.ProductToPart_ID IS NULL
        THEN SUM(bp.Planned_Qty)
        ELSE MAX(bp.Planned_Qty)
    END AS Planned,

    MAX(bp.BatchProduct_Comments) AS Comment

, SUM(
    CASE 
        WHEN bp.ProductToPart_ID IS NULL 
        THEN COALESCE(sm.Sold, 0)
        ELSE 0
    END
) AS Sold

, SUM(
    CASE 
        WHEN bp.ProductToPart_ID IS NULL 
        THEN COALESCE(sm.AssemblyDone, 0)
        ELSE 0
    END
) AS Done

, CASE 
    WHEN MAX(CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END) = 1
    THEN MAX(dt.DetailsTotal)
    ELSE 0
END AS DetailsTotal

, CASE
    WHEN bp.ProductToPart_ID IS NOT NULL
    THEN 1
    ELSE MAX(COALESCE(ch.ChildCount, 0))
END AS DetailsChildTotal
, CASE 
    WHEN MAX(CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END) = 1
    THEN MAX(dstat.DetailsChildDone)
    ELSE 0
END AS DetailsDone
, CASE
    WHEN bp.ProductToPart_ID IS NOT NULL
    THEN
        CASE
            WHEN dtask.DetailFinishChild IS NOT NULL
            THEN 1
            ELSE 0
        END
    ELSE COALESCE(dstat.DetailsChildDone,0)
END AS DetailsChildDone
, CASE
    WHEN bp.ProductToPart_ID IS NOT NULL
    THEN dtask.DetailStartChild
    ELSE dtask.DetailStart
END AS DetailStart

, CASE
    WHEN bp.ProductToPart_ID IS NOT NULL
    THEN dtask.DetailFinishChild
    ELSE dtask.DetailFinish
END AS DetailFinish
, dtask.DetailFinishChildList

, CASE 
    WHEN MAX(CASE WHEN bp.ProductToPart_ID IS NULL THEN 1 ELSE 0 END) = 0
         AND MAX(COALESCE(ch.ChildCount, 0)) > 0
    THEN 1 ELSE 0
END AS IsReadOnlyChild

, CASE
    WHEN NOT EXISTS (
        SELECT 1
        FROM tasks t
        WHERE t.BatchProduct_ID IN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.IsActive = 1
        )
        AND t.IsActive = 1
        AND t.Tasks_Status <> 3
    )
    THEN 1 ELSE 0
END AS IsCompleted

, CASE

    WHEN NOT EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        WHERE t.BatchProduct_ID IN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.IsActive = 1
        )
        AND t.IsActive = 1
        AND ts.Step_Type = 1
    )
    THEN ''

    WHEN NOT EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        WHERE t.BatchProduct_ID IN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.IsActive = 1
        )
        AND t.IsActive = 1
        AND ts.Step_Type = 1
        AND t.Tasks_Status <> 5
    )
    THEN 'NotStarted'

    ELSE ''
END AS DetailStatus

, CASE

WHEN bp.ProductToPart_ID IS NOT NULL
THEN ''

    WHEN NOT EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        WHERE t.BatchProduct_ID IN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.IsActive = 1
        )
        AND t.IsActive = 1
        AND ts.Step_Type = 2
    )
    THEN ''

    WHEN NOT EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        WHERE t.BatchProduct_ID IN (
            SELECT bp2.ID
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.IsActive = 1
        )
        AND t.IsActive = 1
        AND ts.Step_Type = 2
        AND t.Tasks_Status <> 5
    )
    THEN 'NotStarted'

WHEN EXISTS (
    SELECT 1
    FROM tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    WHERE t.BatchProduct_ID IN (
        SELECT bp2.ID
        FROM batches_products bp2
        WHERE bp2.Batch_Id = bp.Batch_Id
          AND bp2.Version_Id = bp.Version_Id
          AND bp2.IsActive = 1
    )
    AND t.IsActive = 1
    AND ts.Step_Type = 2
    AND t.Tasks_Status IN (1,5)
)
THEN 'CanStart'

    ELSE ''
END AS AssemblyStatus

FROM (
    SELECT
        bp.*,
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

    FROM batches_products bp
    WHERE bp.IsActive = 1
) bp
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN categories c ON c.ID = p.Category_ID

-- STOCK MOVEMENTS

LEFT JOIN (
    SELECT
        BatchProduct_ID,
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS Sold,
        SUM(CASE WHEN Move_Type = 'ASSEMBLY' THEN Stock_Qty ELSE 0 END)
        -
        SUM(CASE WHEN Move_Type = 'SOLD' THEN ABS(Stock_Qty) ELSE 0 END) AS AssemblyDone
    FROM stock_movements
    WHERE IsActive = 1
    GROUP BY BatchProduct_ID
) sm ON sm.BatchProduct_ID = bp.ID
AND bp.ProductToPart_ID IS NULL

-- DETAILS TOTALS

LEFT JOIN (
    SELECT
        v.ID AS VersionId,
        COUNT(DISTINCT ptp.ID) AS DetailsTotal
    FROM versions v
    JOIN producttopparts ptp 
        ON ptp.Version_ID = v.ID
        AND ptp.IsActive = 1
    JOIN toppartsteps ts
        ON ts.ProductToPart_ID = ptp.ID
        AND ts.IsActive = 1
    JOIN stage_step_type_map m
        ON m.Step_Type_ID = ts.Step_Type
        AND m.Stage = 1
        AND m.IsActive = 1
    WHERE ptp.IsActive = 1
    GROUP BY v.ID
) dt ON dt.VersionId = bp.Version_Id

-- CHILD COUNTS

LEFT JOIN (
    SELECT
        bp.Batch_Id,
        bp.Version_Id,
        COUNT(DISTINCT bp.ProductToPart_ID) AS ChildCount
    FROM batches_products bp
    WHERE bp.ProductToPart_ID IS NOT NULL
      AND bp.IsActive = 1
    GROUP BY bp.Batch_Id, bp.Version_Id
) ch 
ON ch.Batch_Id = bp.Batch_Id 
AND ch.Version_Id = bp.Version_Id


-- DETAIL TASK STATUS / DATES

LEFT JOIN (
    SELECT
        bp.RootId,

        MIN(CASE 
            WHEN t.Tasks_Status IN (2,3) AND t.Started_At IS NOT NULL
            THEN t.Started_At 
        END) AS DetailStart,

CASE
    WHEN NOT EXISTS (
    SELECT 1
    FROM tasks t2
    JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID

    WHERE t2.BatchProduct_ID IN (
        SELECT bp2.ID
        FROM batches_products bp2
        WHERE bp2.Batch_Id = bp.Batch_Id
          AND bp2.Version_Id = bp.Version_Id
          AND bp2.IsActive = 1
    )

      AND t2.IsActive = 1

      AND ts2.Step_Type = 1
      AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)

      AND ts2.Step_Order <= (
            SELECT MIN(ts3.Step_Order)
            FROM toppartsteps ts3
            WHERE ts3.ProductToPart_ID = ts2.ProductToPart_ID
              AND ts3.IsFinal = 1
              AND (ts3.IsPainting = 0 OR ts3.IsPainting IS NULL)
      )

      AND t2.Tasks_Status <> 3
)
    THEN MAX(
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM toppartsteps ts
                WHERE ts.ID = t.TopPartStep_ID
                  AND ts.Step_Type = 1
            )
            THEN t.Finished_At
        END
    )
    ELSE NULL
END AS DetailFinish,

 GROUP_CONCAT(
    DISTINCT CASE
        WHEN bp.ProductToPart_ID IS NOT NULL
            AND EXISTS (
                SELECT 1
                FROM batches_products bp2
                WHERE bp2.Batch_Id = bp.Batch_Id
                AND bp2.Version_Id = bp.Version_Id
                AND bp2.ProductToPart_ID IS NULL
                AND bp2.IsActive = 1
            )
            AND NOT EXISTS (
    SELECT 1
    FROM tasks t2
    JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID

    WHERE t2.BatchProduct_ID = bp.ID
      AND t2.IsActive = 1

      AND ts2.Step_Type = 1
      AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)

      AND ts2.Step_Order <= (
            SELECT MIN(ts3.Step_Order)
            FROM toppartsteps ts3
            WHERE ts3.ProductToPart_ID = ts2.ProductToPart_ID
              AND ts3.IsFinal = 1
              AND (ts3.IsPainting = 0 OR ts3.IsPainting IS NULL)
      )

      AND t2.Tasks_Status <> 3
)
        THEN CONCAT(
                bp.ProductToPart_ID, ':',
                bp.ID, ':',
                DATE_FORMAT((
                        SELECT MAX(t3.Finished_At)
                        FROM tasks t3
                        JOIN toppartsteps ts3 ON ts3.ID = t3.TopPartStep_ID
                        WHERE t3.BatchProduct_ID = bp.ID
                        AND t3.IsActive = 1
                        AND ts3.Step_Type = 1
                        AND ts3.IsPainting = 0
                        AND ts3.IsFinal = 1
                    ), '%Y-%m-%d'),
                    ':',
                    bp.Planned_Qty
                )
        ELSE NULL
    END
) AS DetailFinishChildList,

MAX(
    CASE
        WHEN bp.ProductToPart_ID IS NOT NULL
         AND NOT EXISTS (
            SELECT 1
            FROM tasks t2
            JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID

            WHERE t2.BatchProduct_ID = bp.ID
              AND t2.IsActive = 1
              AND ts2.Step_Type = 1
              AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)

              AND ts2.Step_Order <= (
                    SELECT MIN(ts3.Step_Order)
                    FROM toppartsteps ts3
                    WHERE ts3.ProductToPart_ID = ts2.ProductToPart_ID
                      AND ts3.IsFinal = 1
                      AND (ts3.IsPainting = 0 OR ts3.IsPainting IS NULL)
              )

              AND t2.Tasks_Status <> 3
        )
        THEN (
            SELECT MAX(t3.Finished_At)
            FROM tasks t3
            JOIN toppartsteps ts3 ON ts3.ID = t3.TopPartStep_ID

            WHERE t3.BatchProduct_ID = bp.ID
              AND t3.IsActive = 1
              AND ts3.Step_Type = 1
              AND ts3.IsPainting = 0
              AND ts3.IsFinal = 1
        )
        ELSE NULL
    END
) AS DetailFinishChild,

MIN(
    CASE
        WHEN bp.ProductToPart_ID IS NOT NULL
         AND EXISTS (
            SELECT 1
            FROM tasks t2
            JOIN toppartsteps ts2 ON ts2.ID = t2.TopPartStep_ID

            WHERE t2.BatchProduct_ID = bp.ID
              AND t2.IsActive = 1
              AND ts2.Step_Type = 1
              AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)
         )
        THEN (
            SELECT MIN(t3.Started_At)
            FROM tasks t3
            JOIN toppartsteps ts3 ON ts3.ID = t3.TopPartStep_ID

            WHERE t3.BatchProduct_ID = bp.ID
              AND t3.IsActive = 1
              AND ts3.Step_Type = 1
              AND ts3.IsPainting = 0
        )
        ELSE NULL
    END
) AS DetailStartChild

    FROM (
        SELECT
            bp.*,
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
        FROM batches_products bp
        WHERE bp.IsActive = 1
    ) bp

    LEFT JOIN tasks t 
            ON t.BatchProduct_ID IN (
                SELECT bp2.ID
                FROM batches_products bp2
                WHERE bp2.Batch_Id = bp.Batch_Id
                AND bp2.Version_Id = bp.Version_Id
                AND bp2.IsActive = 1
            )
            AND t.IsActive = 1

    GROUP BY bp.RootId
) dtask ON dtask.RootId = bp.RootId


-- DETAIL DONE STATISTICS

LEFT JOIN (
    SELECT
        bp.Batch_Id,
        bp.Version_Id,

        -- Parent gatavie (visi step=3)
SUM(
    CASE 
        WHEN bp.ProductToPart_ID IS NULL
         AND EXISTS (
             SELECT 1
             FROM batches_products bp2
             WHERE bp2.Batch_Id = bp.Batch_Id
               AND bp2.Version_Id = bp.Version_Id
               AND bp2.IsActive = 1
         )
         AND NOT EXISTS (
             SELECT 1
             FROM tasks t
             WHERE t.BatchProduct_ID IN (
                 SELECT bp2.ID
                 FROM batches_products bp2
                 WHERE bp2.Batch_Id = bp.Batch_Id
                   AND bp2.Version_Id = bp.Version_Id
                   AND bp2.IsActive = 1
             )
             AND t.IsActive = 1

            AND EXISTS (
                    SELECT 1
                    FROM toppartsteps ts
                    WHERE ts.ID = t.TopPartStep_ID
                    AND ts.Step_Type = 1
                    AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)
                )

             AND t.Tasks_Status <> 3
         )
        THEN 1 ELSE 0
    END
) AS DetailsDone,

-- Child gatavie
        SUM(
            CASE 
                WHEN bp.ProductToPart_ID IS NOT NULL
AND CASE 
    WHEN EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID

        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1
          AND ts.Step_Type = 1
          AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)
    )

    AND NOT EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID

        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1

          AND ts.Step_Type = 1
          AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)

          AND ts.Step_Order <= (
                SELECT MIN(ts2.Step_Order)
                FROM toppartsteps ts2
                WHERE ts2.ProductToPart_ID = ts.ProductToPart_ID
                AND ts2.IsFinal = 1
                AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)
            )

          AND ts.Step_Order <= (
                SELECT MIN(ts2.Step_Order)
                FROM toppartsteps ts2
                WHERE ts2.ProductToPart_ID = ts.ProductToPart_ID
                  AND ts2.IsFinal = 1
                  AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)
          )

          AND t.Tasks_Status <> 3
    )

    THEN 1 ELSE 0
END
                THEN 1 ELSE 0
            END
        ) AS DetailsChildDone

    FROM batches_products bp
    WHERE bp.IsActive = 1
    GROUP BY bp.Batch_Id, bp.Version_Id
) dstat
ON dstat.Batch_Id = bp.Batch_Id
AND dstat.Version_Id = bp.Version_Id

WHERE bp.IsActive = 1
  AND b.IsActive = 1
  AND b.Batches_Statuss = 1

  AND (
        bp.ProductToPart_ID IS NULL

        OR NOT EXISTS (
            SELECT 1
            FROM batches_products bp2
            WHERE bp2.Batch_Id = bp.Batch_Id
              AND bp2.Version_Id = bp.Version_Id
              AND bp2.ProductToPart_ID IS NULL
              AND bp2.IsActive = 1
        )

        OR (
    bp.ProductToPart_ID IS NOT NULL

    AND EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID

        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1
          AND ts.Step_Type = 1
          AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)
    )

    AND NOT EXISTS (
        SELECT 1
        FROM tasks t
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID

        WHERE t.BatchProduct_ID = bp.ID
          AND t.IsActive = 1
          AND ts.Step_Type = 1
          AND (ts.IsPainting = 0 OR ts.IsPainting IS NULL)

          AND ts.Step_Order <= (
                SELECT MIN(ts2.Step_Order)
                FROM toppartsteps ts2
                WHERE ts2.ProductToPart_ID = ts.ProductToPart_ID
                  AND ts2.IsFinal = 1
                  AND (ts2.IsPainting = 0 OR ts2.IsPainting IS NULL)
          )

          AND t.Tasks_Status <> 3
    )
)
      )

GROUP BY
    bp.Batch_Id,
    bp.Version_Id,
    bp.ProductToPart_ID

ORDER BY b.ID DESC;
";

cmd.CommandText = sql;

var list = new List<ProductionBatchRowDto>();

await using var r = await cmd.ExecuteReaderAsync();

var ordBatchId = r.GetOrdinal("BatchId");
var ordBatchCode = r.GetOrdinal("BatchCode");
var ordBatchProductId = r.GetOrdinal("BatchProductId");
var ordVersionId = r.GetOrdinal("VersionId");
var ordProductName = r.GetOrdinal("ProductName");
var ordProductCode = r.GetOrdinal("ProductCode");
var ordCategoryName = r.GetOrdinal("CategoryName");
var ordProductToPartId = r.GetOrdinal("ProductToPartId");
var ordPlanned = r.GetOrdinal("Planned");
var ordSold = r.GetOrdinal("Sold");
var ordDone = r.GetOrdinal("Done");
var ordIsCompleted = r.GetOrdinal("IsCompleted");
var ordDetailStatus = r.GetOrdinal("DetailStatus");
var ordAssemblyStatus = r.GetOrdinal("AssemblyStatus");
var ordDetailStart = r.GetOrdinal("DetailStart");
var ordDetailFinish = r.GetOrdinal("DetailFinish");
var ordComment = r.GetOrdinal("Comment");
var ordHiddenDetailNames = r.GetOrdinal("HiddenDetailNames");
var ordDetailFinishChildList = r.GetOrdinal("DetailFinishChildList");
var ordDetailName = r.GetOrdinal("DetailName");
var ordCategoryId = r.GetOrdinal("CategoryId");
var ordParentCategoryId = r.GetOrdinal("ParentCategoryId");
var ordVersionName = r.GetOrdinal("VersionName");
var ordVersionIsActive = r.GetOrdinal("VersionIsActive");
var ordProductionModel = r.GetOrdinal("ProductionModel");

var ordIsPriority = r.GetOrdinal("IsPriority");

var ordDetailsTotal = r.GetOrdinal("DetailsTotal");
var ordDetailsChildTotal = r.GetOrdinal("DetailsChildTotal");
var ordDetailsDone = r.GetOrdinal("DetailsDone");
var ordDetailsChildDone = r.GetOrdinal("DetailsChildDone");

var ordIsReadOnlyChild = r.GetOrdinal("IsReadOnlyChild");

while (await r.ReadAsync())
{
    var row = new ProductionBatchRowDto
{
        // BASIC INFO
        BatchId = r.GetInt32(ordBatchId),
        BatchCode = r.GetString(ordBatchCode),
        BatchProductId = r.GetInt32(ordBatchProductId),
        VersionId = r.GetInt32(ordVersionId),

        // PRODUCT INFO
        ProductName = r.GetString(ordProductName),
        ProductCode = r.GetString(ordProductCode),
        CategoryName = r.GetString(ordCategoryName),
        ProductToPartId = r.IsDBNull(ordProductToPartId)
            ? (int?)null
            : r.GetInt32(ordProductToPartId),
        CategoryId = r.GetInt32(ordCategoryId),

        ParentCategoryId = r.IsDBNull(ordParentCategoryId)
            ? (int?)null
            : r.GetInt32(ordParentCategoryId),

        VersionName = r.GetString(ordVersionName),
        VersionIsActive = r.GetBoolean(ordVersionIsActive),
        ProductionModel = r.GetInt32(ordProductionModel),
        DetailName = r.IsDBNull(ordDetailName)
            ? null
            : r.GetValue(ordDetailName).ToString(),
        HiddenDetailNames = GetNullableString(r, ordHiddenDetailNames),
        IsPriority = r.GetBoolean(ordIsPriority),

        // PRODUCTION STATUS
        Planned = r.GetInt32(ordPlanned),
        Comment = GetNullableString(r, ordComment),
        Sold = r.GetInt32(ordSold),
        Done = r.GetInt32(ordDone),
        DetailsTotal = r.GetInt32(ordDetailsTotal),
        DetailsChildTotal = r.GetInt32(ordDetailsChildTotal),
        DetailsDone = r.GetInt32(ordDetailsDone),
        DetailsChildDone = r.GetInt32(ordDetailsChildDone),

        // DETAIL STATUS
        DetailStart = GetNullableDateTime(r, ordDetailStart),
        DetailFinish = GetNullableDateTime(r, ordDetailFinish),

        DetailFinishChildList = GetNullableString(r, ordDetailFinishChildList),
        IsReadOnlyChild = r.GetInt32(ordIsReadOnlyChild) == 1,
        IsCompleted = r.GetInt32(ordIsCompleted) == 1,
        DetailStatus = r.GetString(ordDetailStatus),
        AssemblyStatus = r.GetString(ordAssemblyStatus)
        };

    list.Add(row);
}

return list;
}

private static DateTime? GetNullableDateTime(DbDataReader r, int ordinal)
{
    return r.IsDBNull(ordinal)
        ? null
        : r.GetDateTime(ordinal);
}

private static string? GetNullableString(DbDataReader r, int ordinal)
{
    return r.IsDBNull(ordinal)
        ? null
        : r.GetString(ordinal);
}

}


