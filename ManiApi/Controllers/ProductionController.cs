// Šis kontrolieris ir paredzēts ražošanas procesu pārvaldībai: ražošanas uzdevumu pārvaldībai, ražošanas uzdevumu saraksta un detaļas skatīšanai, kā arī ražošanas uzdevumu izveidei un rediģēšanai.

using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;
namespace ManiApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductionController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductionController(AppDbContext db) => _db = db;

    [HttpGet("overview-by-versions")]
    public async Task<IActionResult> GetOverview([FromQuery] List<int> versionIds)
    {
        if (versionIds == null || versionIds.Count == 0)
            return Ok(new List<object>());

        var data = await _db.StockMovements
            .Where(x => x.IsActive && versionIds.Contains(x.Version_ID))
            .GroupBy(x => new { x.Version_ID, x.Move_Type })
            .Select(g => new
            {
                VersionId = g.Key.Version_ID,
                MoveType = g.Key.Move_Type,
                Qty = g.Sum(x => x.Stock_Qty)
            })
            .ToListAsync();

        var result = versionIds.Select(id => new
        {
            VersionId = id,
            Assembly  = data.Where(x => x.VersionId == id && x.MoveType == MoveType.ASSEMBLY).Sum(x => x.Qty),
            Finishing = data.Where(x => x.VersionId == id && x.MoveType == MoveType.FINISHING).Sum(x => x.Qty),
            Stock     = data.Where(x => x.VersionId == id && x.MoveType == MoveType.STOCK).Sum(x => x.Qty)
        });

        return Ok(result);
    }

[HttpGet("gantt")]
public async Task<IActionResult> GetGantt([FromQuery] int? batchProductId)
{

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
    SELECT
        t.ID AS TaskId,
        bp.is_priority,
        bp.Priority,
        t.Tasks_Status,
        bp.ID AS BatchProductId,
        ts.Step_Order,
        ts.Step_Name,
        ts.Step_Type,
        tp.TopPart_Name,
        ts.WorkCentr_ID,
        wc.WorkCentr_Name,
        wc.Capacity,
        t.Assigned_To,
        e.Employee_Name,
        t.Finished_At,

    CASE 
        WHEN ts.Step_Type IN (1,2) 
            THEN (bp.Planned_Qty * ptp.Qty_Per_product) * COALESCE(ts.Estimated_Minutes, 12)
        WHEN ts.Step_Type = 3 
            THEN t.Qty_Done * COALESCE(ts.Estimated_Minutes, 12)
        ELSE bp.Planned_Qty * COALESCE(ts.Estimated_Minutes, 12)
    END AS EstimatedTotalMinutes,

(
    SELECT COALESCE(SUM(
        CASE 
            WHEN ts2.Step_Type IN (1,2)
                THEN (bp.Planned_Qty * ptp.Qty_Per_product) * COALESCE(ts2.Estimated_Minutes, 12)
            WHEN ts2.Step_Type = 3
                THEN t.Qty_Done * COALESCE(ts2.Estimated_Minutes, 12)
            ELSE bp.Planned_Qty * COALESCE(ts2.Estimated_Minutes, 12)
        END
    ),0)
FROM toppartsteps ts2
WHERE ts2.ProductToPart_ID = ts.ProductToPart_ID
  AND ts2.IsActive = 1
  AND ts2.Step_Order < ts.Step_Order
) AS EstimatedStartMinutes,

(
    SELECT COALESCE(SUM(s.DurationMinutes),0)
    FROM tasks_work_sessions s
    WHERE s.Task_ID = t.ID
) AS ActualMinutes 

        FROM tasks t
        JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
        JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
        LEFT JOIN workcentr_type wc ON wc.ID = ts.WorkCentr_ID
        LEFT JOIN employees e ON e.ID = t.Assigned_To
        JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
        JOIN toppart tp ON tp.ID = ptp.TopPart_ID
        WHERE t.IsActive = 1
        AND ts.IsActive = 1
        AND (@bp IS NULL OR t.BatchProduct_ID = @bp)
        ORDER BY 
            bp.is_priority DESC,
            bp.Priority ASC,
            t.Tasks_Priority DESC,
            bp.ID ASC,
            ts.Step_Order ASC;
        ";
    cmd.Parameters.Add(new MySqlConnector.MySqlParameter("@bp", batchProductId));

    var list = new List<object>();

    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            TaskId = r.GetInt32(0),
            IsPriority = r.GetBoolean(1),
            Priority = r.GetInt32(2),
            Status = r.GetInt32(3),
            BatchProductId = r.GetInt32(4),
            StepOrder = r.GetInt32(5),
            StepName  = r.IsDBNull(6) ? null : r.GetString(6),
            StepType  = r.IsDBNull(7) ? 0 : r.GetInt32(7),
            PartName  = r.IsDBNull(8) ? null : r.GetString(8),
            WorkCenterId = r.IsDBNull(9) ? (int?)null : r.GetInt32(9),
            WorkCenterName = r.IsDBNull(10) ? null : r.GetString(10),
            Capacity = r.IsDBNull(11) ? 1 : r.GetInt32(11),
            AssignedTo = r.IsDBNull(12) ? (int?)null : r.GetInt32(12),
            EmployeeName = r.IsDBNull(13) ? null : r.GetString(13),
            FinishedAt = r.IsDBNull(14) ? (DateTime?)null : r.GetDateTime(14),
            EstimatedTotalMinutes = r.IsDBNull(15) ? 0 : r.GetInt32(15),
            EstimatedStartMinutes = r.IsDBNull(16) ? 0 : r.GetInt32(16),
            ActualMinutes = r.IsDBNull(17) ? 0 : r.GetInt32(17)
        });
    }

    return Ok(list);
}

}