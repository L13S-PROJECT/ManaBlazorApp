using System.Data.Common;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using ManiApi.Services.Tasks;
using ManiApi.Data;

namespace ManiApi.Services.ProductionFlows.ParentInline;

public sealed class InlineParentDetailService
{
    private readonly AppDbContext _db;
    private readonly TaskQueryService _queryService;

    public InlineParentDetailService(
        AppDbContext db,
        TaskQueryService queryService)
    {
        _db = db;
        _queryService = queryService;
    }

    public async Task HandleParentDetailStep(
    DbConnection conn,
    DbTransaction tx,
    int taskId,
    int batchProductId,
    int rootId)
        {

            await using (var upd = conn.CreateCommand())
                {
                    upd.Transaction = tx;

                    upd.CommandText = @"
                    UPDATE tasks t
                    JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                    SET t.Tasks_Status = 3,
                        t.Finished_At  = CURRENT_TIMESTAMP
                    WHERE t.ID = @id;";

                    upd.Parameters.Add(new MySqlParameter("@id", taskId));

                    await upd.ExecuteNonQueryAsync();
                }

            bool detailFinished =
                await _queryService.IsDetailPhaseFinishedAll(
                    conn,
                    tx,
                    rootId
                );
            
            if (detailFinished)
                {
                    bool alreadyDone = await _queryService.HasDetailedMovement(
                        conn, tx, batchProductId);

                    if (!alreadyDone)
                    {
                        var versionId = await _db.BatchProducts
                            .Where(x => x.ID == batchProductId)
                            .Select(x => x.Version_Id)
                            .FirstAsync();

                        var plannedQty = await _db.BatchProducts
                            .Where(x => x.ID == batchProductId)
                            .Select(x => x.Planned_Qty)
                            .FirstAsync();

                        await using var move = conn.CreateCommand();

                        move.Transaction = tx;

                        move.CommandText = @"
                INSERT INTO stock_movements
                (
                    Version_ID,
                    BatchProduct_ID,
                    Move_Type,
                    Stock_Qty,
                    Created_At,
                    Task_ID,
                    IsActive
                )
                VALUES
                (@ver, @bpId, 'PLANNED',  -@qty, CURRENT_TIMESTAMP, @taskId, 1),
                (@ver, @bpId, 'DETAILED',  @qty, CURRENT_TIMESTAMP, @taskId, 1);";

                        move.Parameters.Add(new MySqlParameter("@ver", versionId));
                        move.Parameters.Add(new MySqlParameter("@bpId", batchProductId));
                        move.Parameters.Add(new MySqlParameter("@qty", plannedQty));
                        move.Parameters.Add(new MySqlParameter("@taskId", taskId));

                        await move.ExecuteNonQueryAsync();
                    }
                }

        }

}