using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Data;
using ManiApi.DTOs.Tasks;
using ManiApi.Models;

namespace ManiApi.Services.Tasks
{
    public class TaskManagementService
    {
        private readonly AppDbContext _db;

        public TaskManagementService(AppDbContext db)
        {
            _db = db;
        }
    
//Atjaunina vairākus uzdevumus (tasks) datubāzē, mainot tikai tos laukus, kas ir norādīti.
    public async Task<int> UpdateSteps(List<UpdateStepDto> steps)
{
    if (steps == null || steps.Count == 0)
        throw new Exception("Nav neviena soļa, ko atjaunināt.");

    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var tx = await conn.BeginTransactionAsync();

        try
        {
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

            if (dto.Tasks_Push.HasValue)
                {
                    setParts.Add("Tasks_Push = @push");
                }

    // Assigned_To vienmēr iekļaujam (arī NULL gadījumā)
            if (dto.Assigned_To != null)
                {
                    setParts.Add("Assigned_To = @assigned");
                }
            
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
                    if (dto.Assigned_To != null)
                        {
                            var pAssigned = cmd.CreateParameter();
                            pAssigned.ParameterName = "@assigned";
                            pAssigned.Value = dto.Assigned_To;
                            cmd.Parameters.Add(pAssigned);
                        }

                    if (dto.Tasks_Push.HasValue)
                    {
                        var pPush = cmd.CreateParameter();
                        pPush.ParameterName = "@push";
                        pPush.Value = dto.Tasks_Push.Value ? 1 : 0;
                        cmd.Parameters.Add(pPush);
                    }

                var affected = await cmd.ExecuteNonQueryAsync();
                totalUpdated += affected;
            }

            await tx.CommitAsync();

            return totalUpdated;
    }
                catch
    {
        await tx.RollbackAsync();
        throw;
    }

}

// Maina uz statusu 1 TIKAI šai partijai + detaļai, un tikai no 5. 
//Šis ir tas, ko sauc par "aktivizēšanu" (activate) – kad darbinieks sāk strādāt pie konkrētā soļa, 
//mēs aktivizējam visus ar šo partiju un detaļu saistītos uzdevumus, kas ir gaidīšanas (5) režīmā. 
//Tas ļauj darbiniekam redzēt visus uzdevumus, kas jāveic, un sistēmai saprast, ka šie uzdevumi ir "aktīvi" un tiek strādāti pie tiem.
public async Task<int> ActivatePart(ActivatePartDto dto)
{
       
    if (dto is null || dto.BatchProductId <= 0 || dto.ProductToPartId <= 0)
        throw new Exception("BatchId un ProductToPartId ir obligāti.");

    var conn = _db.Database.GetDbConnection();
    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
cmd.CommandText = $@"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Tasks_Status = 1
WHERE t.IsActive = 1
  AND t.Tasks_Status IN (5)
  {GetBatchProductFilterSql()}
  AND ts.ProductToPart_ID = @ptp
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

    System.Diagnostics.Debug.WriteLine($"ACTIVATE RESULT: updated rows = {affected}");

    return affected;
}

//Atgriež visus unikālos ProductToPart_ID, 
//kuriem konkrētajā batch (ar to pašu Batch_Id un Version_Id) ir aktīvi uzdevumi ar statusu 1, 2 vai 3.
public async Task<List<int>> GetActiveParts(int batchProductId)
{
    if (batchProductId <= 0)
        throw new Exception("batchProductId is required.");

    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();
cmd.CommandText = $@"
SELECT ts.ProductToPart_ID
FROM tasks t
JOIN toppartsteps ts       ON ts.ID = t.TopPartStep_ID
JOIN producttopparts ptp   ON ptp.ID = ts.ProductToPart_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status IN (1,2,3)
  AND ptp.IsActive = 1
  {GetBatchProductFilterSql()}
  GROUP BY ts.ProductToPart_ID;
";

    var pBatch = cmd.CreateParameter();
    pBatch.ParameterName = "@bp";
    pBatch.Value = batchProductId;
    cmd.Parameters.Add(pBatch);

    var list = new List<int>();
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        list.Add(r.GetInt32(0));
    }

    return list;
}

public async Task<int> SetPartPriority(SetPartPriorityDto dto)
{
    if (dto is null || dto.BatchProductId <= 0 || dto.ProductToPartId <= 0)
        throw new Exception("BatchProductId un ProductToPartId ir obligāti.");

    var conn = _db.Database.GetDbConnection();
    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    // DEBUG: paskatāmies, kādi ProductToPart_ID atbilst šim filtram
    await using var cmdDbg = conn.CreateCommand();
    cmdDbg.CommandText = @"
SELECT DISTINCT ts.ProductToPart_ID
FROM tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  AND t.BatchProduct_ID = @bp
  AND ts.ProductToPart_ID = @ptp;
";
    cmdDbg.Parameters.Add(new MySqlConnector.MySqlParameter("@bp", dto.BatchProductId));
    cmdDbg.Parameters.Add(new MySqlConnector.MySqlParameter("@ptp", dto.ProductToPartId));

    // UPDATE: uzliek/noņem prioritāti visiem status=1 soļiem šai detaļai šajā batchProduct
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $@"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Tasks_Priority = @prio
WHERE t.IsActive = 1
  AND t.Tasks_Status = 1
  {GetBatchProductFilterSql()}
  AND ts.ProductToPart_ID = @ptp;
";
    cmd.Parameters.Add(new MySqlConnector.MySqlParameter("@prio", dto.Tasks_Priority ? 1 : 0));
    cmd.Parameters.Add(new MySqlConnector.MySqlParameter("@bp", dto.BatchProductId));
    cmd.Parameters.Add(new MySqlConnector.MySqlParameter("@ptp", dto.ProductToPartId));

    var affected = await cmd.ExecuteNonQueryAsync();

    return affected;
}

public async Task<int> SetTaskPush(SetTaskPushDto dto)
{
    if (dto == null || dto.BatchProductId <= 0 || dto.ProductToPartId <= 0)
        throw new Exception("Invalid data.");

    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

    using var cmd = conn.CreateCommand();

    cmd.CommandText = $@"
    UPDATE tasks t
    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
    SET t.Tasks_Push = @push
    WHERE t.IsActive = 1
      {GetBatchProductFilterSql()}
      AND ts.ProductToPart_ID = @ptp;
    ";

    var p1 = cmd.CreateParameter();
    p1.ParameterName = "@push";
    p1.Value = dto.Tasks_Push;
    cmd.Parameters.Add(p1);

    var p2 = cmd.CreateParameter();
    p2.ParameterName = "@bp";
    p2.Value = dto.BatchProductId;
    cmd.Parameters.Add(p2);

    var p3 = cmd.CreateParameter();
    p3.ParameterName = "@ptp";
    p3.Value = dto.ProductToPartId;
    cmd.Parameters.Add(p3);

    return await cmd.ExecuteNonQueryAsync();
}

//Atjaunina konkrētam aktīvam uzdevumam (tasks tabulā) lauku Assigned_To pēc TaskId 
//un atgriež, cik rindas tika izmainītas (vai kļūdu, ja nekas netika atrasts).
public async Task<int> UpdateAssignee(UpdateTaskAssigneeDto dto)
{
    if (dto is null || dto.TaskId <= 0)
        throw new Exception("TaskId is required.");

    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
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
        throw new Exception("Task not found or inactive.");

    return affected;
}

//Atjaunina (vai noņem) komentāru visiem aktīvajiem uzdevumiem ar to pašu soli (TopPartStep_ID) 
//un tajā pašā batch + version grupā kā dotais TaskId.

public async Task<int> UpdateComment(UpdateCommentDto dto)
{
    if (dto is null || dto.TaskId <= 0)
        throw new Exception("TaskId is required.");

    var baseTask = await _db.Tasks
    .Where(x => x.ID == dto.TaskId && x.IsActive)
    .Select(x => new { x.TopPartStep_ID, x.BatchProduct_ID })
    .FirstOrDefaultAsync();

if (baseTask is null)
    throw new Exception("Task not found");

var bpInfo = await _db.Set<BatchProduct>()
    .Where(b => b.ID == baseTask.BatchProduct_ID)
    .Select(b => new { b.Batch_Id, b.Version_Id })
    .FirstOrDefaultAsync();

if (bpInfo is null)
    throw new Exception("BatchProduct not found");


var tasks = await _db.Tasks
    .Where(x =>
        x.IsActive &&
        x.TopPartStep_ID == baseTask.TopPartStep_ID &&
        _db.Set<BatchProduct>().Any(bp =>
            bp.ID == x.BatchProduct_ID &&
            bp.IsActive &&
            bp.Batch_Id == bpInfo.Batch_Id &&
            bp.Version_Id == bpInfo.Version_Id
        ))
    .ToListAsync();

foreach (var t in tasks)
{
    t.Tasks_Comment = string.IsNullOrWhiteSpace(dto.Comment)
        ? null
        : dto.Comment;
}

await _db.SaveChangesAsync();

    return tasks.Count;
}

//Šis kods ļauj detail skatā nomainīt darbinieku visiem uzdevumiem, 
//kas pieder konkrētajam solim un detaļai izvēlētajā ražošanas partijā - JA IR APVIENOTS PARENT+CHILD scenārijs.
public async Task<int> UpdateAssigneeRoot(UpdateAssigneeDto dto)
{
    if (dto.BatchProductId <= 0)
        throw new Exception("BatchProductId is required.");

    var conn = _db.Database.GetDbConnection();
    
    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    await using var cmd = conn.CreateCommand();

    cmd.CommandText = @"
UPDATE tasks t
JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
JOIN batches_products bp0 ON bp0.ID = @bpId

SET t.Assigned_To = @empId

WHERE bp.Batch_Id = bp0.Batch_Id
AND bp.Version_Id = bp0.Version_Id
AND t.TopPartStep_ID = @stepId
AND EXISTS (
    SELECT 1
    FROM toppartsteps ts
    WHERE ts.ID = t.TopPartStep_ID
      AND ts.ProductToPart_ID = @partId
)
AND t.IsActive = 1;
";

    var p1 = cmd.CreateParameter();
    p1.ParameterName = "@empId";
    p1.Value = (object?)dto.Assigned_To ?? DBNull.Value;
    cmd.Parameters.Add(p1);

    var p2 = cmd.CreateParameter();
    p2.ParameterName = "@bpId";
    p2.Value = dto.BatchProductId;
    cmd.Parameters.Add(p2);

    var p3 = cmd.CreateParameter();
    p3.ParameterName = "@stepId";
    p3.Value = dto.TopPartStepId;
    cmd.Parameters.Add(p3);

    var p4 = cmd.CreateParameter();
    p4.ParameterName = "@partId";
    p4.Value = dto.ProductToPartId;
    cmd.Parameters.Add(p4);

    var affected = await cmd.ExecuteNonQueryAsync();

    return affected;
}

//Šis kods atjaunina (UPDATE) visiem aktīvajiem un vēl nesāktajiem (Tasks_Status = 1) 
//uzdevumiem konkrētā batch + versijā un konkrētā solī (TopPartStep_ID) piešķirto darbinieku (Assigned_To).

public async Task<int> UpdateAssigneeAggregated(UpdateAssigneeAggregatedDto dto)
{
    if (dto is null || dto.BatchProductId <= 0 || dto.ProductToPartId <= 0)
        throw new Exception("Invalid input.");

    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

    var batchInfo = await _db.BatchProducts
        .Where(x => x.ID == dto.BatchProductId)
        .Select(x => new { x.Batch_Id, x.Version_Id })
        .FirstOrDefaultAsync();

    if (batchInfo == null)
        throw new Exception("Batch not found.");

    await using var cmd = conn.CreateCommand();
if (dto.RowType == "SingleChild")
{
    cmd.CommandText = @"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Assigned_To = @emp
WHERE t.IsActive = 1
AND t.Tasks_Status = 1
AND t.BatchProduct_ID IN (
    SELECT bp2.ID
    FROM batches_products bp2
    WHERE bp2.IsActive = 1
        AND bp2.Batch_Id = @batchId
        AND bp2.Version_Id = @versionId
)
AND ts.ID = @step;
";
}

else

{
    // Parent vai Parent+ChildMerged
    cmd.CommandText = @"
UPDATE tasks t
JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
SET t.Assigned_To = @emp
WHERE t.IsActive = 1
AND t.Tasks_Status = 1
AND t.BatchProduct_ID IN (
    SELECT bp2.ID
    FROM batches_products bp2
    WHERE bp2.IsActive = 1
        AND bp2.Batch_Id = @batchId
        AND bp2.Version_Id = @versionId
)
AND ts.ID = @step
";
}

    cmd.Parameters.Add(new MySqlParameter("@emp", (object?)dto.Assigned_To ?? DBNull.Value));
    cmd.Parameters.Add(new MySqlParameter("@batchId", batchInfo.Batch_Id));
    cmd.Parameters.Add(new MySqlParameter("@versionId", batchInfo.Version_Id));
    cmd.Parameters.Add(new MySqlParameter("@step", dto.TopPartStepId));

    var affected = await cmd.ExecuteNonQueryAsync();

    Console.WriteLine($"UPDATED ROWS: {affected}");

    return affected;
    
}


//Šis kods vienā SQL piegājienā atjaunina vairākiem uzdevumiem (tasks) piešķirto darbinieku (Assigned_To), 
//balstoties uz sarakstu ar TaskId → Assigned_To vērtībām

public async Task UpdateAssigneeBulk(List<UpdateAssigneeRequest> list)
{
    if (list == null || list.Count == 0)
        
        return;
    list = list.DistinctBy(x => x.TaskId).ToList();

    var conn = _db.Database.GetDbConnection();

    if (conn.State != ConnectionState.Open)
        await conn.OpenAsync();

await using var cmd = conn.CreateCommand();

var cases = new List<string>();
var ids = new List<int>();
int i = 0;

foreach (var item in list)
{
    cases.Add($"WHEN {item.TaskId} THEN @emp{i}");
    cmd.Parameters.Add(new MySqlParameter($"@emp{i}", (object?)item.Assigned_To ?? DBNull.Value));
    ids.Add(item.TaskId);
    i++;
}

if (ids.Count == 0)
    return;

cmd.CommandText = $@"
UPDATE tasks
SET Assigned_To = CASE ID
    {string.Join(" ", cases)}
END
WHERE ID IN ({string.Join(",", ids)});
";

await cmd.ExecuteNonQueryAsync();

}

//HELPERIS..

private string GetBatchProductFilterSql()
{
    return @"
AND t.BatchProduct_ID IN (
    SELECT bp2.ID
    FROM batches_products bp2
    JOIN batches_products bp0 ON bp0.ID = @bp
    WHERE bp2.IsActive = 1
      AND bp2.Batch_Id = bp0.Batch_Id
      AND bp2.Version_Id = bp0.Version_Id
)";
}

}
}