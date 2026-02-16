using Microsoft.AspNetCore.Mvc;
using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using ManiApi.Models;


namespace ManiApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly AppDbContext _db;
        public TasksController(AppDbContext db) => _db = db;

        // GET: /api/tasks/for-employee?empId=101
        // Rāda: Prioritārie (Tasks_Priority=1) ar statusu 1 (nav iesākts) + paša iesāktie (statuss=2)
// GET: /api/tasks/for-employee?empId=101


[HttpGet("for-employee")]
public async Task<IActionResult> GetForEmployee(
    [FromQuery] int empId = 1,
    [FromQuery] int workcentrId = 0
)

{
    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
cmd.CommandText = @"
SELECT
  t.ID,                      -- 0 TaskId
  t.Tasks_Priority,          -- 1 Priority
  bp.is_priority AS BatchPriority,
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

  CASE 
    WHEN t.Is_Comment_For_Employee = 1 
    THEN t.Tasks_Comment 
    ELSE NULL 
  END AS Comment,             -- 6 Comment

  p.Product_Name,            -- 7 ProductName
  tp.TopPart_Name,           -- 8 PartName
  ts.Step_Name,              -- 9 StepName
  b.Batches_Code,            -- 10 BatchCode


 CASE 
    WHEN ts.Step_Type IN (1,2) THEN bp.Planned_Qty * ptp.Qty_Per_product
    WHEN ts.Step_Type = 3      THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS PlannedForTask,        -- 9 Planned

  COALESCE(t.Qty_Done, 0) AS DoneForTask, -- 10 Done

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
  AND t.Tasks_Status IN (1,2)
  AND (t.Assigned_To = @empId OR t.Assigned_To = 0)
ORDER BY
  PriorityLevel DESC,
  t.Tasks_Status,
  ts.Step_Order,
  t.ID DESC;
";


    // Šobrīd empId vēl neizmantojam filtrēšanai, bet parametru paturam nākotnei
    var pEmp = cmd.CreateParameter();
    pEmp.ParameterName = "@empId";
    var pWc = cmd.CreateParameter();
            pWc.ParameterName = "@workcentrId";
            pWc.Value = workcentrId;
            cmd.Parameters.Add(pWc);

    pEmp.Value = empId;
    cmd.Parameters.Add(pEmp);

    var list = new List<object>();
   { await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
            list.Add(new
                {
                    TaskId      = r.GetInt32(0),
                    Priority    = r.IsDBNull(1) ? (byte)0 : r.GetByte(1),
                    BatchPriority = r.GetBoolean(2),
                    Status        = r.GetInt32(3),
                    PriorityLevel = r.IsDBNull(4) ? 0 : r.GetInt32(4),
                    StartedAt   = r.IsDBNull(5) ? (DateTime?)null : r.GetDateTime(5 ),
                    FinishedAt  = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6),

                    IsCommentForEmployee = !r.IsDBNull(7) && r.GetBoolean(7),
                    Comment     = r.IsDBNull(8) ? null : r.GetString(8),

                    ProductName = r.IsDBNull(9) ? null : r.GetString(9),
                    PartName    = r.IsDBNull(10) ? null : r.GetString(10),
                    StepName    = r.IsDBNull(11) ? null : r.GetString(11),
                    BatchCode   = r.IsDBNull(12) ? null : r.GetString(12),

                    Planned     = r.IsDBNull(13) ? 0 : r.GetInt32(13),
                    Done        = r.IsDBNull(14) ? 0 : r.GetInt32(14),
                    StepOrder   = r.IsDBNull(15) ? 0 : r.GetInt32(15),

                    StepType       = r.IsDBNull(16) ? 0 : r.GetInt32(16),
                    BatchId        = r.IsDBNull(17) ? 0 : r.GetInt32(17),
                    VersionId      = r.IsDBNull(18) ? 0 : r.GetInt32(18),
                    BatchProductId = r.IsDBNull(19) ? 0 : r.GetInt32(19)
                });
    }
   }
   
    return Ok(list);
}

        // POST: /api/tasks/claim   body: { "taskId": 123, "empId": 101 }
// Atzīmē “SĀKT”: aizliedz, ja darbiniekam jau ir kāds status=2.
[HttpPost("claim")]
public async Task<IActionResult> Claim([FromBody] ClaimDto dto)
{
    if (dto is null || dto.TaskId <= 0 || dto.EmpId <= 0)
        return BadRequest();

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();
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
        pEmp.Value = dto.EmpId;
        chk.Parameters.Add(pEmp);

        var cnt = Convert.ToInt32(await chk.ExecuteScalarAsync());
        if (cnt > 0)
        {
            await tx.RollbackAsync();
            return Conflict("Jau ir iesākts cits darbs.");
        }
    }

    // 2) Pārejam uz statusu 2 šim taskam
    await using (var upd = conn.CreateCommand())
    {
        upd.Transaction = tx;
        upd.CommandText = @"
UPDATE tasks 
   SET Tasks_Status = 2, 
       Claimed_By   = @emp,
       Started_At   = CURRENT_TIMESTAMP
 WHERE ID = @taskId 
   AND Tasks_Status = 1 
   AND IsActive = 1;";

        var pEmp = upd.CreateParameter();
        pEmp.ParameterName = "@emp";
        pEmp.Value = dto.EmpId;
        upd.Parameters.Add(pEmp);

        var pId = upd.CreateParameter();
        pId.ParameterName = "@taskId";
        pId.Value = dto.TaskId;
        upd.Parameters.Add(pId);

        var affected = await upd.ExecuteNonQueryAsync();
        if (affected == 0)
        {
            await tx.RollbackAsync();
            return NotFound("Darbs vairs nav pieejams.");
        }
    }

    // 3) Ja šis ir FINISHING solis (Step_Type = 3) un ir norādīts apjoms,
    //    veicam kustību ASSEMBLY -> FINISHING stock_movements (idempotenti).
    int stepType = 0;
    int batchProductId = 0;
    int versionId = 0;
    int finishingQty = 0;

    await using (var info = conn.CreateCommand())
    {
        info.Transaction = tx;
        info.CommandText = @"
SELECT 
    ts.Step_Type,
    t.BatchProduct_ID,
    bp.Version_Id,
    COALESCE(t.Qty_Done, 0) AS FinishingQty
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID AND bp.IsActive = 1
WHERE t.ID = @id
  AND t.IsActive = 1;";

        var p = info.CreateParameter();
        p.ParameterName = "@id";
        p.Value = dto.TaskId;
        info.Parameters.Add(p);

        await using var r = await info.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            stepType = r.GetInt32(0);
            batchProductId = r.GetInt32(1);
            versionId = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            finishingQty = r.IsDBNull(3) ? 0 : r.GetInt32(3);
        }
    }

    // Tikai, ja tas ir Finishing solis un ir jēgpilns apjoms
    if (stepType == 3 && versionId > 0 && batchProductId > 0 && finishingQty > 0)
    {
        // ✅ Idempotence: ja šim taskam jau ir ielikts FINISHING +qty (no Claim),
        // tad neliekam vēlreiz (citādi Assembly kļūs vēl negatīvāks).
        int alreadyMoved = 0;
        await using (var chkMove = conn.CreateCommand())
        {
            chkMove.Transaction = tx;
            chkMove.CommandText = @"
SELECT COUNT(*)
FROM stock_movements
WHERE IsActive = 1
  AND Task_ID = @taskId
  AND BatchProduct_ID = @bpId
  AND Version_ID = @ver
  AND Move_Type = 'FINISHING'
  AND Stock_Qty > 0;";

            var pTask = chkMove.CreateParameter();
            pTask.ParameterName = "@taskId";
            pTask.Value = dto.TaskId;
            chkMove.Parameters.Add(pTask);

            var pBp = chkMove.CreateParameter();
            pBp.ParameterName = "@bpId";
            pBp.Value = batchProductId;
            chkMove.Parameters.Add(pBp);

            var pVer = chkMove.CreateParameter();
            pVer.ParameterName = "@ver";
            pVer.Value = versionId;
            chkMove.Parameters.Add(pVer);

            alreadyMoved = Convert.ToInt32(await chkMove.ExecuteScalarAsync());
        }

        if (alreadyMoved == 0)
        {
            await using (var cmdMove = conn.CreateCommand())
            {
                cmdMove.Transaction = tx;
                cmdMove.CommandText = @"
INSERT INTO stock_movements 
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'ASSEMBLY',  -@qty, CURRENT_TIMESTAMP, @taskId, 1),
    (@ver, @bpId, 'FINISHING',  @qty, CURRENT_TIMESTAMP, @taskId, 1);";

                var pVer2 = cmdMove.CreateParameter();
                pVer2.ParameterName = "@ver";
                pVer2.Value = versionId;
                cmdMove.Parameters.Add(pVer2);

                var pBp2 = cmdMove.CreateParameter();
                pBp2.ParameterName = "@bpId";
                pBp2.Value = batchProductId;
                cmdMove.Parameters.Add(pBp2);

                var pQty = cmdMove.CreateParameter();
                pQty.ParameterName = "@qty";
                pQty.Value = finishingQty;
                cmdMove.Parameters.Add(pQty);

                var pTask2 = cmdMove.CreateParameter();
                pTask2.ParameterName = "@taskId";
                pTask2.Value = dto.TaskId;
                cmdMove.Parameters.Add(pTask2);

                await cmdMove.ExecuteNonQueryAsync();
            }
        }
    }

    await tx.CommitAsync();
    return Ok(new { claimed = true, taskId = dto.TaskId, empId = dto.EmpId });
}


/// POST: /api/tasks/finish   body: { "taskId": 123, "qtyDoneAdd": 5 }
[HttpPost("finish")]
public async Task<IActionResult> Finish([FromBody] FinishDto dto)
{
    if (dto is null || dto.TaskId <= 0)
        return BadRequest("TaskId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    int currentStatus;

    // 1) Nolasām statusu un nolockojam rindu
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
        p.Value = dto.TaskId;
        cmd.Parameters.Add(p);

        var obj = await cmd.ExecuteScalarAsync();
        if (obj == null || obj == DBNull.Value)
        {
            await tx.RollbackAsync();
            return NotFound("Uzdevums nav atrasts vai ir neaktīvs.");
        }

        currentStatus = Convert.ToInt32(obj);
    }

    // 2) Atļaujam pabeigt tikai, ja ir 'Procesā' (2)
    if (currentStatus != 2)
    {
        await tx.RollbackAsync();
        return BadRequest("Pabeigt drīkst tikai uzdevumu ar statusu 'Procesā'.");
    }

    // 3) Nolasām Step_Type, Qty_Per_product, PlannedQty, CurrentDone, BatchProductId, VersionId
    int stepType;
    int qtyPerProduct;
    int plannedQty;
    int currentDone;
    int batchProductId;
    int versionId;

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
    COALESCE(t.Qty_Scrap, 0)         AS FinishingPlannedQty
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
        p.Value = dto.TaskId;
        info.Parameters.Add(p);

        await using var rr = await info.ExecuteReaderAsync();
        if (!await rr.ReadAsync())
        {
            await tx.RollbackAsync();
            return NotFound("Uzdevuma dati nav atrasti.");
        }

        stepType       = rr.GetInt32(0);
        qtyPerProduct  = rr.GetInt32(1);
        plannedQty     = rr.GetInt32(2);
        currentDone    = rr.GetInt32(3);
        batchProductId = rr.GetInt32(4);
        versionId      = rr.IsDBNull(5) ? 0 : rr.GetInt32(5);
        var finishingPlannedQty = rr.IsDBNull(6) ? 0 : rr.GetInt32(6);

        // Ja Finishing solis – pārrakstām plannedQty ar to, ko iedeva Finishing popup
        if (stepType == 3 && finishingPlannedQty > 0)
        {
            plannedQty = finishingPlannedQty;
        }
    }

    int newStatus  = 2;
    int newDoneOut = currentDone;

    // 4) Detailed / Assembly – pabeidzam VISU uzreiz
    if (stepType == 1 || stepType == 2)
    {
        var qtyDone = plannedQty * qtyPerProduct;

        await using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = @"
UPDATE tasks
   SET Tasks_Status = 3,
       Finished_At  = CURRENT_TIMESTAMP,
       Qty_Done     = @qtyDone
 WHERE ID = @id;";
            var p1 = upd.CreateParameter(); p1.ParameterName = "@qtyDone"; p1.Value = qtyDone;    upd.Parameters.Add(p1);
            var p2 = upd.CreateParameter(); p2.ParameterName = "@id";      p2.Value = dto.TaskId; upd.Parameters.Add(p2);
            await upd.ExecuteNonQueryAsync();
        }

        newStatus  = 3;
        newDoneOut = qtyDone;

        // 4.1) Detailed īpašais gadījums – kad VISI Detailed soļi pabeigti -> PLANNED -> DETAILED + atvērt Assembly
        if (stepType == 1 && batchProductId > 0)
        {
            int notFinishedDetailed = 0;
            await using (var cmdCheck = conn.CreateCommand())
            {
                cmdCheck.Transaction = tx;
                cmdCheck.CommandText = @"
SELECT COUNT(*)
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.BatchProduct_ID = @bpId
  AND t.IsActive = 1
  AND ts.Step_Type = 1
  AND t.Tasks_Status <> 3;";

                var pBp = cmdCheck.CreateParameter();
                pBp.ParameterName = "@bpId";
                pBp.Value = batchProductId;
                cmdCheck.Parameters.Add(pBp);

                var objCnt = await cmdCheck.ExecuteScalarAsync();
                notFinishedDetailed = (objCnt == null || objCnt == DBNull.Value)
                    ? 0
                    : Convert.ToInt32(objCnt);
            }

            if (notFinishedDetailed == 0)
            {
                // atveram Assembly (5 -> 1)
                await using (var cmdOpenAsm = conn.CreateCommand())
                {
                    cmdOpenAsm.Transaction = tx;
                    cmdOpenAsm.CommandText = @"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Tasks_Status = 1
WHERE t.BatchProduct_ID = @bpId
  AND t.IsActive = 1
  AND ts.Step_Type = 2
  AND t.Tasks_Status = 5;";

                    var pBp2 = cmdOpenAsm.CreateParameter();
                    pBp2.ParameterName = "@bpId";
                    pBp2.Value = batchProductId;
                    cmdOpenAsm.Parameters.Add(pBp2);

                    await cmdOpenAsm.ExecuteNonQueryAsync();
                }

                // PLANNED -> DETAILED (vienreiz per BatchProduct)
                if (versionId > 0)
                {
                    int existingDetailed = 0;
                    await using (var cmdCheckMove = conn.CreateCommand())
                    {
                        cmdCheckMove.Transaction = tx;
                        cmdCheckMove.CommandText = @"
SELECT COUNT(*)
FROM stock_movements
WHERE BatchProduct_ID = @bpId
  AND Move_Type = 'DETAILED'
  AND IsActive = 1;";

                        var pM = cmdCheckMove.CreateParameter();
                        pM.ParameterName = "@bpId";
                        pM.Value = batchProductId;
                        cmdCheckMove.Parameters.Add(pM);

                        var objM = await cmdCheckMove.ExecuteScalarAsync();
                        existingDetailed = (objM == null || objM == DBNull.Value)
                            ? 0
                            : Convert.ToInt32(objM);
                    }

                    if (existingDetailed == 0)
                    {
                        var totalQty = plannedQty * qtyPerProduct;

                        // PLANNED -
                        await using (var m1 = conn.CreateCommand())
                        {
                            m1.Transaction = tx;
                            m1.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, IsActive)
VALUES
    (@ver, @bpId, 'PLANNED', -@qty, CURRENT_TIMESTAMP, 1);";

                            m1.Parameters.Add(new MySqlParameter("@ver",  versionId));
                            m1.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
                            m1.Parameters.Add(new MySqlParameter("@qty",  totalQty));

                            await m1.ExecuteNonQueryAsync();
                        }

                        // DETAILED +
                        await using (var m2 = conn.CreateCommand())
                        {
                            m2.Transaction = tx;
                            m2.CommandText = @"
INSERT INTO stock_movements
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'DETAILED', @qty, CURRENT_TIMESTAMP, @taskId, 1);";

                            m2.Parameters.Add(new MySqlParameter("@ver",    versionId));
                            m2.Parameters.Add(new MySqlParameter("@bpId",   batchProductId));
                            m2.Parameters.Add(new MySqlParameter("@qty",    totalQty));
                            m2.Parameters.Add(new MySqlParameter("@taskId", dto.TaskId));

                            await m2.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        // 4.2) Assembly īpašais gadījums – kad VISI Assembly soļi pabeigti -> DETAILED -> ASSEMBLY
        if (stepType == 2 && batchProductId > 0 && versionId > 0)
        {
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

                        var pBp3 = cmdMove.CreateParameter();
                        pBp3.ParameterName = "@bpId";
                        pBp3.Value = batchProductId;
                        cmdMove.Parameters.Add(pBp3);

                        var pQty = cmdMove.CreateParameter();
                        pQty.ParameterName = "@qty";
                        pQty.Value = totalQty;
                        cmdMove.Parameters.Add(pQty);

                        var pTask = cmdMove.CreateParameter();
                        pTask.ParameterName = "@taskId";
                        pTask.Value = dto.TaskId;
                        cmdMove.Parameters.Add(pTask);

                        await cmdMove.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
    else
    {
        // 5) Finishing — apjoms jau ir Qty_Done (no popup), šeit tikai statusu pabeidzam + kustību uz STOCK.

        // 5.0) Task -> Finished
        await using (var upd = conn.CreateCommand())
        {
            upd.Transaction = tx;
            upd.CommandText = @"
UPDATE tasks
   SET Tasks_Status = 3,
       Finished_At  = CURRENT_TIMESTAMP
 WHERE ID = @id;";
            upd.Parameters.Add(new MySqlParameter("@id", dto.TaskId));
            await upd.ExecuteNonQueryAsync();
        }

// 5.1) FINISHING -> STOCK kustība (idempotenta)
var qtyMove = currentDone;
if (qtyMove > 0 && batchProductId > 0 && versionId > 0)
{
    // ja STOCK jau ir ielikts šim taskam -> neko nedaram (idempotence)
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
        chk.Parameters.Add(new MySqlParameter("@taskId", dto.TaskId));
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
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'FINISHING', -@qty, CURRENT_TIMESTAMP, @taskId, 1),
    (@ver, @bpId, 'STOCK',      @qty, CURRENT_TIMESTAMP, @taskId, 1);";

            mv.Parameters.Add(new MySqlParameter("@ver", versionId));
            mv.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
            mv.Parameters.Add(new MySqlParameter("@qty", qtyMove));
            mv.Parameters.Add(new MySqlParameter("@taskId", dto.TaskId));

            await mv.ExecuteNonQueryAsync();
        }
    }
}

        newStatus  = 3;
        newDoneOut = currentDone;
    }

    await tx.CommitAsync();
    return Ok(new { taskId = dto.TaskId, status = newStatus, done = newDoneOut });
}

        public sealed class FinishDto
{
    public int TaskId { get; set; }

    // Tikai Finishing gadījumam:
    // cik gabalus darbinieks pabeidza šajā reizē
    public int? QtyDoneAdd { get; set; }
}

        public sealed class ClaimDto
        {
            public int TaskId { get; set; }
            public int EmpId  { get; set; }
        }

// POST: /api/tasks/update-steps
// Body: [ { "taskId": 123, "tasks_Priority": true, "assigned_To": 101 }, ... ]
[HttpPost("update-steps")]
public async Task<IActionResult> UpdateSteps([FromBody] List<UpdateStepDto> steps)
{
    if (steps == null || steps.Count == 0)
        return BadRequest("Nav neviena soļa, ko atjaunināt.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var tx = await conn.BeginTransactionAsync();

    int totalUpdated = 0;

    foreach (var dto in steps)
    {
        if (dto == null || dto.TaskId <= 0)
            continue;

        // Dinamiski būvējam SET daļu atkarībā no tā, kas patiešām jāmaina
        var setParts = new List<string>();

        if (dto.Tasks_Priority.HasValue)
        {
            setParts.Add("Tasks_Priority = @prio");
        }

        if (dto.Assigned_To.HasValue)
        {
            setParts.Add("Assigned_To = @assigned");
        }

        // Ja nav ko mainīt – ejam tālāk
        if (setParts.Count == 0)
            continue;

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        cmd.CommandText = $@"
UPDATE tasks
   SET {string.Join(", ", setParts)}
 WHERE ID = @id
   AND IsActive = 1;";

        // obligāta – kurš tasks
        var pId = cmd.CreateParameter();
        pId.ParameterName = "@id";
        pId.Value = dto.TaskId;
        cmd.Parameters.Add(pId);

        // ja jāmaina prioritāte
        if (dto.Tasks_Priority.HasValue)
        {
            var pPrio = cmd.CreateParameter();
            pPrio.ParameterName = "@prio";
            // Tasks_Priority ir TINYINT(1) NOT NULL → vienmēr 0 vai 1
            pPrio.Value = dto.Tasks_Priority.Value ? 1 : 0;
            cmd.Parameters.Add(pPrio);
        }

        // ja jāmaina Assigned_To (var būt arī null -> noņem assignment)
        if (dto.Assigned_To.HasValue)
        {
            var pAss = cmd.CreateParameter();
            pAss.ParameterName = "@assigned";
            pAss.Value = dto.Assigned_To.Value;
            cmd.Parameters.Add(pAss);
        }

        var affected = await cmd.ExecuteNonQueryAsync();
        totalUpdated += affected;
    }

    await tx.CommitAsync();

    return Ok(new { updated = totalUpdated });
}



// POST: /api/tasks/activate-part
// Maina uz statusu 1 TIKAI šai partijai + detaļai, un tikai no 5.
[HttpPost("activate-part")]
public async Task<IActionResult> ActivatePart([FromBody] ActivatePartDto dto)
{
    Console.WriteLine(
    $"[activate-part] BatchProductId={dto.BatchProductId}, ProductToPartId={dto.ProductToPartId}"
);
    
    if (dto is null || dto.BatchProductId <= 0 || dto.ProductToPartId <= 0)
    return BadRequest("BatchId un ProductToPartId ir obligāti.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
cmd.CommandText = @"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Tasks_Status = 1
WHERE t.IsActive = 1
  AND t.Tasks_Status = 5
  AND t.BatchProduct_ID = @bp
  AND ts.ProductToPart_ID = @ptp;
";

    var pBp = cmd.CreateParameter();
pBp.ParameterName = "@bp";
pBp.Value = dto.BatchProductId;
cmd.Parameters.Add(pBp);

var pPtp = cmd.CreateParameter();
pPtp.ParameterName = "@ptp";
pPtp.Value = dto.ProductToPartId;
cmd.Parameters.Add(pPtp);


    var affected = await cmd.ExecuteNonQueryAsync();

    return Ok(new { updated = affected });
}

// POST: /api/tasks/open-finishing
// DTO no Blazor → API, lai atvērtu Finishing tasku
public sealed class OpenFinishingDto
{
    // ŠEIT arī strādājam ar BatchProductId
    public int BatchProductId { get; set; }
    public int ProductToPartId { get; set; }
    public int Qty { get; set; }
    public string? Comment { get; set; }
}


// Ko atdodam atpakaļ Blazoram
public sealed class OpenFinishingResultDto
{
    public int TaskId { get; set; }
}

[HttpPost("open-finishing")]
public async Task<IActionResult> OpenFinishing([FromBody] OpenFinishingDto dto)
{
Console.WriteLine(
  $"[open-finishing] bpId={dto.BatchProductId}, ptpId={dto.ProductToPartId}, qty={dto.Qty}, comment='{dto.Comment}'");


    if (dto.BatchProductId <= 0 || dto.ProductToPartId <= 0 || dto.Qty <= 0)
        return BadRequest("BatchProductId, ProductToPartId un Qty ir obligāti, Qty > 0.");

    // drošībai – transakcija
    await using var tx = await _db.Database.BeginTransactionAsync();

    // 1) atrodam FINISHING soli šai detaļai
    var finishingStep = await _db.TopPartSteps
        .FirstOrDefaultAsync(ts =>
            ts.ProductToPartId == dto.ProductToPartId &&
            ts.StepType == 3 &&
            ts.IsActive);

    if (finishingStep is null)
    {
        await tx.RollbackAsync();
        return BadRequest("Šai detaļai nav definēts Finishing solis (StepType = 3).");
    }

    var batchProductId = dto.BatchProductId;

    // ASSEMBLY reālais stock
var assemblyStock = await _db.StockMovements
    .Where(x =>
        x.IsActive &&
        x.BatchProduct_ID == batchProductId &&
        x.Move_Type == MoveType.ASSEMBLY)
    .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

// Jau rezervēts Finishing (status=1) – vēl nav sācies, bet apjoms vairs nav brīvs
var reservedForFinishing = await _db.Tasks
    .Join(_db.TopPartSteps,
          t => t.TopPartStep_ID,
          ts => ts.Id,
          (t, ts) => new { t, ts })
    .Where(x =>
        x.t.IsActive &&
        x.t.BatchProduct_ID == batchProductId &&
        x.ts.StepType == 3 &&
        x.t.Tasks_Status == 1 &&
        x.t.Qty_Done > 0)
    .SumAsync(x => (int?)x.t.Qty_Done) ?? 0;

// Pieejams jaunam Finishing vilnim
var assemblyAvailable = Math.Max(assemblyStock - reservedForFinishing, 0);



    // 2) visi gaidošie (status 5) Finishing taski šai partijai + detaļai
    var waitingTasks = await _db.Tasks
        .Where(t =>
            t.IsActive &&
            t.BatchProduct_ID == batchProductId &&
            t.TopPartStep_ID  == finishingStep.Id &&
            t.Tasks_Status    == 5)
        .OrderBy(t => t.ID)
        .ToListAsync();

    ManiApi.Models.Tasks activeTask;

    if (waitingTasks.Count == 0)
    {
        // 3a) NAV gaidoša taska → vienkārši veidojam jaunu vilnīti
        activeTask = new ManiApi.Models.Tasks
{
    BatchProduct_ID = batchProductId,
    TopPartStep_ID  = finishingStep.Id,
    Tasks_Status    = 1,
    IsActive        = true,
    Qty_Done        = dto.Qty,
    Qty_Scrap       = 0,
    Tasks_Comment   = dto.Comment // ✅
};

        _db.Tasks.Add(activeTask);
        await _db.SaveChangesAsync();
    }
    else
    {
        // 3b) Ir vismaz viens gaidošais (status 5) – ņemam pirmo kā "parentu"
        var parent = waitingTasks[0];
        var planned = assemblyAvailable;      // kopējais Assembly šim parentam
        var delta   = dto.Qty;              // šī vilnīša apjoms

        if (planned <= 0 || delta >= planned)
        {
            // Pilns vilnis vai parentam nav jēdzīga qty:
            // 5 -> 1 un izmantojam šo pašu rindu
            parent.Tasks_Status  = 1;
parent.Qty_Done      = delta > 0 ? delta : planned;
parent.Tasks_Comment = dto.Comment; // ✅ PIEVIENO


            // ja ar kādām kļūdām bija vairāk "gaidošo" – deaktivējam tos
            foreach (var extra in waitingTasks.Skip(1))
                extra.IsActive = false;

            await _db.SaveChangesAsync();

            activeTask = parent;
        }
        else
        {
            // Reāla DALĪŠANA: delta < planned
            var remaining = planned - delta;

            // Oriģinālo atzīmējam kā “Dalīts” (status 4), plānu atstājam,
            // lai redzams sākotnējais pieprasījums.
            // status 4 neizmantojam – veco “parent” noņemam no aktīvās aprites
            
            parent.IsActive = false;


            // Jaunais aktīvais vilnītis
           activeTask = new ManiApi.Models.Tasks
{
    BatchProduct_ID = parent.BatchProduct_ID,
    TopPartStep_ID  = parent.TopPartStep_ID,
    Tasks_Status    = 1,
    IsActive        = true,
    Qty_Done        = delta,
    Qty_Scrap       = 0,
    Tasks_Comment   = dto.Comment
};


            _db.Tasks.Add(activeTask);

            // Atlikums – jauns gaidošais (status 5)
           var waitingRemainder = new ManiApi.Models.Tasks
{
    BatchProduct_ID = parent.BatchProduct_ID,
    TopPartStep_ID  = parent.TopPartStep_ID,
    Tasks_Status    = 5,
    IsActive        = true,
    Qty_Done        = remaining,
    Qty_Scrap       = 0
};

            _db.Tasks.Add(waitingRemainder);

            // vecos “gaidošos”, ja tādi ir, deaktivējam
            foreach (var extra in waitingTasks.Skip(1))
                extra.IsActive = false;
            
            await _db.SaveChangesAsync();
        }
    }

    await tx.CommitAsync();

    return Ok(new OpenFinishingResultDto
    {
        TaskId = activeTask.ID
    });
}

/// šo vajag pie ProductioTasks.razor "ķeksim" 5->1
public sealed class ActivatePartDto
{
    public int BatchProductId { get; set; }
    public int ProductToPartId { get; set; }
}


public sealed class UpdateStepDto
{
    // Kurš konkrētais tasks (tasks.ID)
    public int TaskId { get; set; }

    // Vai solis ir prioritārs (var nebūt padots -> atstājam kā ir)
    public bool? Tasks_Priority { get; set; }

    // Kam tiek piešķirts (var būt null -> noņemam Assignment)
    public int? Assigned_To { get; set; }
}

// GET: /api/tasks/active-parts?batchId=123
// Atgriež ProductToPart_ID sarakstu šai partijai ar statusu 1
[HttpGet("active-parts")]
public async Task<IActionResult> GetActiveParts([FromQuery] int batchId)
{
    if (batchId <= 0)
        return BadRequest("batchId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
SELECT DISTINCT ts.ProductToPart_ID
FROM tasks t
JOIN batches_products bp   ON bp.ID = t.BatchProduct_ID
JOIN toppartsteps     ts   ON ts.ID = t.TopPartStep_ID
JOIN producttopparts  ptp  ON ptp.ID = ts.ProductToPart_ID
WHERE t.IsActive      = 1
  AND t.Tasks_Status  IN (1,2,3,4)
  AND bp.Batch_Id     = @batch
  AND ptp.IsActive    = 1;
";

    var pBatch = cmd.CreateParameter();
    pBatch.ParameterName = "@batch";
    pBatch.Value = batchId;
    cmd.Parameters.Add(pBatch);

    var list = new List<int>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(r.GetInt32(0));
    }

    return Ok(list);
}


// GET: /api/tasks/detailed-summary-by-batch?batchId=123
[HttpGet("detailed-summary-by-batch")]
public async Task<IActionResult> GetDetailedSummaryByBatch([FromQuery] int batchId)
{
    if (batchId <= 0)
        return BadRequest("batchId is required.");

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
  AND ts.Step_Type    = 1      -- Detailed
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

    return Ok(list);
}

[HttpGet("finishing-waves")]
public async Task<IActionResult> GetFinishingWaves([FromQuery] int batchProductId, [FromQuery] int productToPartId)
{
    if (batchProductId <= 0 || productToPartId <= 0)
        return BadRequest("batchProductId and productToPartId are required.");

    var list = await _db.Tasks
        .Join(_db.TopPartSteps,
              t => t.TopPartStep_ID,
              ts => ts.Id,
              (t, ts) => new { t, ts })
        .Where(x =>
    x.t.IsActive &&
    x.t.BatchProduct_ID == batchProductId &&
    x.ts.ProductToPartId == productToPartId &&
    x.ts.StepType == 3 &&
    x.t.Tasks_Status != 5          // ✅ ŠIS
)

        .OrderByDescending(x => x.t.ID)
        .Select(x => new
{
    TaskId = x.t.ID,
    Status = x.t.Tasks_Status,
    Planned = x.t.Qty_Done,
    StartedAt = x.t.Started_At,
    FinishedAt = x.t.Finished_At,
    Comment = x.t.Tasks_Comment // ✅
})

        .ToListAsync();

        Console.WriteLine("[finishing-waves] " + string.Join(" | ", list.Select(x => $"{x.TaskId}:{(x.Comment ?? "NULL")}")));


    return Ok(list);
}


// GET: /api/tasks/detailed-summary-by-batchproduct?batchProductId=123
[HttpGet("detailed-summary-by-batchproduct")]
public async Task<IActionResult> GetDetailedSummaryByBatchProduct([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

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

    return Ok(list);
}

[HttpGet("finishing-inprogress-by-version")]
public async Task<IActionResult> GetFinishingInProgressByVersion([FromQuery] int versionId)
{
    var conn = _db.Database.GetDbConnection();
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

    var p = cmd.CreateParameter();
    p.ParameterName = "@vid";
    p.Value = versionId;
    cmd.Parameters.Add(p);

    var val = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    return Ok(new { finishingInProgress = val });
}

[HttpGet("finishing-allocated-by-version")]
public async Task<IActionResult> GetFinishingAllocatedByVersion([FromQuery] int versionId)
{
    var conn = _db.Database.GetDbConnection();
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
  AND t.Tasks_Status IN (2,3);";   // 2=in progress, 3=finished

    var p = cmd.CreateParameter();
    p.ParameterName = "@vid";
    p.Value = versionId;
    cmd.Parameters.Add(p);

    var val = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    return Ok(new { finishingAllocated = val });
}


[HttpPost("update-finishing-qty")]
public async Task<IActionResult> UpdateFinishingQty([FromBody] UpdateFinishingQtyDto dto)
{
   
   Console.WriteLine($"[update-finishing-qty] taskId={dto.TaskId} qty={dto.Qty} comment='{dto.Comment}'");

   
    if (dto is null || dto.TaskId <= 0 || dto.Qty < 0)
        return BadRequest("TaskId un Qty ir obligāti (Qty >= 0).");

    var t = await _db.Tasks.FirstOrDefaultAsync(x => x.ID == dto.TaskId && x.IsActive);
    if (t is null) return NotFound();

    // ja jau sācies vai nav “atvērts” (status=1), labot nedrīkst
    if (t.Started_At != null || t.Tasks_Status != 1)
        return BadRequest("Task already started (vai nav status=1).");

    t.Qty_Done = dto.Qty;
    t.Tasks_Comment = dto.Comment;

    await _db.SaveChangesAsync();
    return Ok(new { updated = true, taskId = t.ID, qty = t.Qty_Done });
}

// GET: /api/tasks/detailed-indicators?batchProductId=123
[HttpGet("detailed-indicators")]
public async Task<IActionResult> GetDetailedIndicators([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var conn = _db.Database.GetDbConnection();
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
  AND ts.Step_Type = 1     -- TIKAI DETAIL posmam

GROUP BY ts.ProductToPart_ID;
";

    var p = cmd.CreateParameter();
    p.ParameterName = "@bp";
    p.Value = batchProductId;
    cmd.Parameters.Add(p);

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

    return Ok(list);
}

// GET: /api/tasks/assembly-indicators?batchProductId=123
[HttpGet("assembly-indicators")]
public async Task<IActionResult> GetAssemblyIndicators([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var conn = _db.Database.GetDbConnection();
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
  AND ts.Step_Type = 2          -- 🔵 TIKAI ASSEMBLY

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

    return Ok(list);
}


// GET: /api/tasks/finishing-indicators?batchProductId=123
[HttpGet("finishing-indicators")]
public async Task<IActionResult> GetFinishingIndicators([FromQuery] int batchProductId)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    // 1) Statusu skaitīšana FINISHING
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
  AND ts.Step_Type = 3       -- !!! Finishing
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

        // 🔑 loģika, par kuru vienojāmies

            string state =
    total == 0
        ? "gray"        // nav vispār finishing tasku
        : cnt3 == total
            ? "green"   // 🔑 VISI finishing taski = 3
            : (cnt1 > 0 || cnt2 > 0)
                ? "yellow"  // iesākts
                : "gray";   // vēl nav sācies


        list.Add(new
        {
            ProductToPartId = r.GetInt32(0),
            State = state
        });
    }

    return Ok(list);
}

[HttpPost("update-comment")]
public async Task<IActionResult> UpdateComment([FromBody] UpdateCommentDto dto)
{
    if (dto is null || dto.TaskId <= 0)
        return BadRequest("TaskId is required.");

    var t = await _db.Tasks.FirstOrDefaultAsync(x => x.ID == dto.TaskId && x.IsActive);
    if (t is null)
        return NotFound();

    t.Tasks_Comment = string.IsNullOrWhiteSpace(dto.Comment)
        ? null
        : dto.Comment;

    await _db.SaveChangesAsync();

    return Ok(new { updated = true, taskId = t.ID });
}


public sealed class UpdateCommentDto
{
    public int TaskId { get; set; }
    public string? Comment { get; set; }
    public bool IsForEmployee { get; set; }

}


public sealed class UpdateFinishingQtyDto
{
    public int TaskId { get; set; }
    public int Qty { get; set; }
    public string? Comment { get; set; }
}

// piešķir konkrētam tasksam konkrētu darbinieku - Assigned_TO
public sealed class UpdateTaskAssigneeDto
{
    public int TaskId { get; set; }
    public int? Assigned_To { get; set; } // null = noņemt
}

[HttpPost("update-assignee")]
public async Task<IActionResult> UpdateAssignee([FromBody] UpdateTaskAssigneeDto dto)
{
    if (dto is null || dto.TaskId <= 0)
        return BadRequest("TaskId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
UPDATE tasks
SET Assigned_To = @emp
WHERE ID = @id
  AND IsActive = 1;
";

    cmd.Parameters.Add(new MySqlParameter("@id", dto.TaskId));
    cmd.Parameters.Add(new MySqlParameter(
        "@emp",
        (object?)dto.Assigned_To ?? DBNull.Value
    ));

    var affected = await cmd.ExecuteNonQueryAsync();
    if (affected == 0)
        return NotFound("Task not found or inactive.");

    return Ok(new { ok = true, taskId = dto.TaskId, assignedTo = dto.Assigned_To });
}

// GET: /api/tasks/by-step?batchProductId=123&topPartStepId=456
[HttpGet("by-step")]
public async Task<IActionResult> GetByStep(
    [FromQuery] int batchProductId,
    [FromQuery] int topPartStepId
)
{
    if (batchProductId <= 0 || topPartStepId <= 0)
        return BadRequest("batchProductId and topPartStepId are required.");

    var conn = _db.Database.GetDbConnection();
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

    return Ok(list);
}

// GET: /api/tasks/by-batch?batchProductId=123&stepType=1
[HttpGet("by-batch")]
public async Task<IActionResult> GetTasksByBatch(
    [FromQuery] int batchProductId,
    [FromQuery] int stepType)
{
    if (batchProductId <= 0)
        return BadRequest("batchProductId is required.");

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
    t.TopPartStep_ID   AS TopPartStepId,
    ptp.ID             AS ProductToPartId,
    t.Started_At,
    t.Finished_At,
    t.Tasks_Comment    AS Comment,   -- ✅ ŠIS
    t.Is_Comment_For_Employee AS IsCommentForEmployee,
    tp.TopPart_Name  AS PartName

FROM tasks t
JOIN toppartsteps    ts  ON ts.ID = t.TopPartStep_ID
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart         tp  ON tp.ID  = ptp.TopPart_ID
WHERE t.IsActive = 1
  AND t.BatchProduct_ID = @bpId
  AND ts.Step_Type = @stepType
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
                StartedAt     = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7),
                FinishedAt    = r.IsDBNull(8) ? (DateTime?)null : r.GetDateTime(8),
                Comment = r.IsDBNull(9) ? null : r.GetString(9),
                IsCommentForEmployee = !r.IsDBNull(10) && r.GetBoolean(10),
                PartName = r.IsDBNull(11) ? null : r.GetString(11)
            });

    }

    return Ok(list);
}

[HttpPost("update-comment-visibility")]
public async Task<IActionResult> UpdateCommentVisibility([FromBody] UpdateCommentVisibilityDto dto)
{
    var t = await _db.Tasks.FirstOrDefaultAsync(x => x.ID == dto.TaskId && x.IsActive);
    if (t is null) return NotFound();

    t.Is_Comment_For_Employee = dto.IsCommentForEmployee;
    await _db.SaveChangesAsync();

    return Ok();
}

public sealed class UpdateCommentVisibilityDto
{
    public int TaskId { get; set; }
    public bool IsCommentForEmployee { get; set; }
}

// GET: /api/tasks/employee-load?empId=123 - 13.02.2026
[HttpGet("employee-load")]
public async Task<IActionResult> GetEmployeeLoad([FromQuery] int empId)
{
    if (empId <= 0)
        return BadRequest("empId is required.");

    var conn = _db.Database.GetDbConnection();
    await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    string? employeeName = null;
string? workCenterName = null;


await using (var cmdHeader = conn.CreateCommand())
{
    cmdHeader.CommandText = @"
SELECT 
    Employee_Name
FROM employees
WHERE ID = @empId;
";

    cmdHeader.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));

    await using var rHeader = await cmdHeader.ExecuteReaderAsync();
    if (await rHeader.ReadAsync())
    {
        employeeName = rHeader.IsDBNull(0) ? null : rHeader.GetString(0);
    }
}

    cmd.CommandText = @"
SELECT
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
    CASE 
    WHEN ts.Step_Type IN (1,2) THEN bp.Planned_Qty * ptp.Qty_Per_product
    WHEN ts.Step_Type = 3 THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS Qty,
    t.Tasks_Status AS Status,
    ts.Step_Order,
    ts.Step_Type,
    ts.ProductToPart_ID,
    tp.TopPart_Name,
    ts.IsFinal,
    t.Assigned_To,
    t.Tasks_Priority,
    t.Claimed_By
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status = 2
  AND t.Claimed_By = @empId;
";

    cmd.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));

    var list = new List<object>();

    await using (var r = await cmd.ExecuteReaderAsync())
{
    while (await r.ReadAsync())
    {
        list.Add(new
        {
            TaskId = r.GetInt32(0),
            BatchProductId = r.GetInt32(1),
            BatchCode = r.GetString(2),
            ProductName = r.GetString(3),
            Qty = r.IsDBNull(4) ? 0 : r.GetInt32(4),
            Status = r.GetInt32(5),
            StepOrder = r.IsDBNull(6) ? 0 : r.GetInt32(6),
            StepType = r.GetInt32(7),
            ProductToPartId = r.GetInt32(8),
            TopPartName = r.IsDBNull(9) ? null : r.GetString(9),
            IsFinal = !r.IsDBNull(10) && r.GetBoolean(10),
            Assigned_To = r.IsDBNull(11) ? (int?)null : r.GetInt32(11),
            Tasks_Priority = !r.IsDBNull(12) && r.GetBoolean(12),
            Claimed_By = r.IsDBNull(13) ? (int?)null : r.GetInt32(13)
        });
    }
}

// PRIORITĀRIE (status = 1, batch priority = true)

await using var cmd2 = conn.CreateCommand();
cmd2.CommandText = @"
SELECT
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
    CASE 
    WHEN ts.Step_Type IN (1,2) THEN bp.Planned_Qty * ptp.Qty_Per_product
    WHEN ts.Step_Type = 3 THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS Qty,
    t.Tasks_Status AS Status,
    ts.Step_Order,
    ts.Step_Type,
    ts.ProductToPart_ID,
    tp.TopPart_Name,
    ts.IsFinal,
    t.Assigned_To,
    t.Tasks_Priority,
    t.Claimed_By
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  AND bp.is_priority = 1
  AND (t.Assigned_To = @empId OR t.Assigned_To = 0);
";

cmd2.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));

var priorityList = new List<object>();

await using (var r2 = await cmd2.ExecuteReaderAsync())
{
    while (await r2.ReadAsync())
    {
        priorityList.Add(new
        {
            TaskId = r2.GetInt32(0),
            BatchProductId = r2.GetInt32(1),
            BatchCode = r2.GetString(2),
            ProductName = r2.GetString(3),
            Qty = r2.IsDBNull(4) ? 0 : r2.GetInt32(4),
            Status = r2.GetInt32(5),
            StepOrder = r2.IsDBNull(6) ? 0 : r2.GetInt32(6),
            StepType = r2.GetInt32(7),
            ProductToPartId = r2.GetInt32(8),
            TopPartName = r2.IsDBNull(9) ? null : r2.GetString(9),
            IsFinal = !r2.IsDBNull(10) && r2.GetBoolean(10),
            Assigned_To = r2.IsDBNull(11) ? (int?)null : r2.GetInt32(11),
            Tasks_Priority = !r2.IsDBNull(12) && r2.GetBoolean(12),
            Claimed_By = r2.IsDBNull(13) ? (int?)null : r2.GetInt32(13)
        });
    }
}
// SECĪGIE (status = 1, batch priority = false)

await using var cmd3 = conn.CreateCommand();
cmd3.CommandText = @"
SELECT
    t.ID AS TaskId,
    t.BatchProduct_ID,
    b.Batches_Code AS BatchCode,
    p.Product_Name AS ProductName,
    CASE 
    WHEN ts.Step_Type IN (1,2) THEN bp.Planned_Qty * ptp.Qty_Per_product
    WHEN ts.Step_Type = 3 THEN t.Qty_Done
    ELSE bp.Planned_Qty
END AS Qty,
    t.Tasks_Status AS Status,
    ts.Step_Order,
    ts.Step_Type,
    ts.ProductToPart_ID,
    tp.TopPart_Name,
    ts.IsFinal,
    t.Assigned_To,
    t.Tasks_Priority,
    t.Claimed_By
FROM tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches b ON b.ID = bp.Batch_Id
JOIN versions v ON v.ID = bp.Version_Id
JOIN products p ON p.ID = v.Product_ID
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
JOIN producttopparts ptp ON ptp.ID = ts.ProductToPart_ID
JOIN toppart tp ON tp.ID = ptp.TopPart_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  AND bp.is_priority = 0
  AND (t.Assigned_To = @empId OR t.Assigned_To = 0);
";

cmd3.Parameters.Add(new MySqlConnector.MySqlParameter("@empId", empId));

var normalList = new List<object>();

await using (var r3 = await cmd3.ExecuteReaderAsync())
{
    while (await r3.ReadAsync())
    {
        normalList.Add(new
        {
            TaskId = r3.GetInt32(0),
            BatchProductId = r3.GetInt32(1),
            BatchCode = r3.GetString(2),
            ProductName = r3.GetString(3),
            Qty = r3.IsDBNull(4) ? 0 : r3.GetInt32(4),
            Status = r3.GetInt32(5),
            StepOrder = r3.IsDBNull(6) ? 0 : r3.GetInt32(6),
            StepType = r3.GetInt32(7),
            ProductToPartId = r3.GetInt32(8),
            TopPartName = r3.IsDBNull(9) ? null : r3.GetString(9),
            IsFinal = !r3.IsDBNull(10) && r3.GetBoolean(10),
            Assigned_To = r3.IsDBNull(11) ? (int?)null : r3.GetInt32(11),
            Tasks_Priority = !r3.IsDBNull(12) && r3.GetBoolean(12),
            Claimed_By = r3.IsDBNull(13) ? (int?)null : r3.GetInt32(13)          
        });
    }
}
    return Ok(new
{
    EmployeeName = employeeName,
    WorkCenterName = workCenterName,
    InProgress = list,
    Priority = priorityList,
    Normal = normalList
});

}

// GET: /api/tasks/steps-for-part?batchProductId=123&productToPartId=8
[HttpGet("steps-for-part")]
public async Task<IActionResult> GetStepsForPart(
    int batchProductId,
    int productToPartId)
{
    var list = await (
        from t in _db.Tasks
        join ts in _db.TopPartSteps
            on t.TopPartStep_ID equals ts.Id

        join ea in _db.Employees
            on t.Assigned_To equals ea.Id into eaJoin
        from ea in eaJoin.DefaultIfEmpty()

        join ec in _db.Employees
            on t.Claimed_By equals ec.Id into ecJoin
        from ec in ecJoin.DefaultIfEmpty()

        where t.IsActive
              && t.BatchProduct_ID == batchProductId
              && ts.ProductToPartId == productToPartId

        orderby ts.StepOrder

        select new
        {
            TaskId = t.ID,
            StepOrder = ts.StepOrder,
            StepName = ts.StepName,
            Status = t.Tasks_Status,

            AssignedName = ea != null ? ea.EmployeeName : null,
            ClaimedName = ec != null ? ec.EmployeeName : null
        }
    ).ToListAsync();

    return Ok(list);
}
    }
}
