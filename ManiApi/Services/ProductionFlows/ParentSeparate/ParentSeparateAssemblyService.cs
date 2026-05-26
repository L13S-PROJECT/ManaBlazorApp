using System.Data.Common;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using ManiApi.Services.Tasks;
using ManiApi.DTOs.Production;
using ManiApi.Data;


namespace ManiApi.Services.ProductionFlows.ParentSeparate;

public sealed class ParentSeparateAssemblyService
{
    private readonly AppDbContext _db;
    private readonly TaskQueryService _queryService;
    public ParentSeparateAssemblyService(
    AppDbContext db,
    TaskQueryService queryService)
            {
                _db = db;
                _queryService = queryService;
            }

    public async Task OpenAssembly(int batchProductId)
{
    var batchProduct = await _db.BatchProducts
        .AsNoTracking()
        .FirstOrDefaultAsync(x =>
            x.ID == batchProductId &&
            x.IsActive);

    if (batchProduct is null)
        throw new Exception("BatchProduct not found.");
    
    var assemblyTasks = await _db.Tasks
        .Where(x =>
            x.BatchProduct_ID == batchProductId &&
            x.IsActive &&
            x.Tasks_Status == 5)
        .Join(_db.TopPartSteps,
            t => t.TopPartStep_ID,
            s => s.Id,
            (t, s) => new { Task = t, Step = s })
        .Where(x => x.Step.StepType == 2)
        .ToListAsync();

    foreach (var row in assemblyTasks)
        {
            row.Task.Tasks_Status = 1;
        }

    await _db.SaveChangesAsync();
}

public async Task HandleAfterDetailFinish(int batchProductId)
{
    await OpenAssembly(batchProductId);
}

public async Task HandleParentAssemblyStep(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int rootId,
    int plannedQty,
    int qtyPerProduct,
    int batchProductId,
    int versionId,
    int currentDone,
    int? ralColorId)
{

bool detailFinished = await CheckDetailFinished(conn, tx, rootId);

    await using (var upd = conn.CreateCommand())
    {
        Console.WriteLine($"UPDATING TASK -> {taskId} TO STATUS=3");
        upd.Transaction = tx;
        upd.CommandText = @"
        UPDATE tasks
            SET Qty_Done = @qty,
                Tasks_Status = 3,
                Finished_At  = CURRENT_TIMESTAMP
            WHERE ID = @id;";

        upd.Parameters.Add(new MySqlParameter("@id", taskId));
        upd.Parameters.Add(new MySqlParameter("@qty", plannedQty));
        await upd.ExecuteNonQueryAsync();
    }

    bool notFinishedAssembly = await _queryService.HasNotFinishedAssembly(conn, tx, rootId);

Console.WriteLine($"ASSEMBLY CHECK -> notFinishedAssembly={notFinishedAssembly}");

    if (!notFinishedAssembly)
    {
    bool existingAsm = await _queryService.HasAssemblyStockMovement(conn, tx, rootId);

        if (!existingAsm)
        {
            var totalQty = plannedQty;

            await using (var cmdMove = conn.CreateCommand())
            {
                cmdMove.Transaction = tx;
                cmdMove.CommandText = @"
INSERT INTO stock_movements 
    (Version_ID, BatchProduct_ID, Move_Type, Stock_Qty, Created_At, Task_ID, IsActive)
VALUES
    (@ver, @bpId, 'DETAILED', -@qty, CURRENT_TIMESTAMP, @taskId, 1),
    (@ver, @bpId, 'ASSEMBLY',  @qty, CURRENT_TIMESTAMP, @taskId, 1);";

                cmdMove.Parameters.Add(new MySqlParameter("@ver", versionId));
                cmdMove.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
                cmdMove.Parameters.Add(new MySqlParameter("@qty", totalQty));
                cmdMove.Parameters.Add(new MySqlParameter("@taskId", taskId));

                await cmdMove.ExecuteNonQueryAsync();
            }
        }
    }

    /*bool isFinalStep = await _queryService.IsFinalStep(conn, tx, taskId);

    if (isFinalStep && detailFinished && !notFinishedAssembly)
    {
        var qtyMove = currentDone;

        if (qtyMove > 0 && batchProductId > 0 && versionId > 0)
        {
        bool alreadyDone = await _queryService.HasStockMovement(conn, tx, taskId, batchProductId, versionId);

            if (!alreadyDone)
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
    }*/

}

private async Task<bool> CheckDetailFinished(DbConnection conn, DbTransaction tx, int rootId)
{
   return await _queryService.IsDetailPhaseFinishedAll(conn, tx, rootId);
}

public TaskDisplayStateDto GetAssemblyDisplayState(
    int status,
    DateTime? startedAt,
    DateTime? finishedAt)
{
    return status switch
    {
        1 => new TaskDisplayStateDto
        {
            CssClass = "ptv3-waiting",
            DisplayDate = null
        },

        2 => new TaskDisplayStateDto
        {
            CssClass = "ptv3-detail-start",
            DisplayDate = startedAt
        },

        3 => new TaskDisplayStateDto
        {
            CssClass = "ptv3-detail-finish",
            DisplayDate = finishedAt
        },

        _ => new TaskDisplayStateDto()
    };
}

}