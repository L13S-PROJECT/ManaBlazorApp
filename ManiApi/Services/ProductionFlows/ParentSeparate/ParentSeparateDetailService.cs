using System.Data.Common;
using MySqlConnector;
using Microsoft.EntityFrameworkCore;
using ManiApi.Services.Tasks;
using ManiApi.Data;

namespace ManiApi.Services.ProductionFlows.ParentSeparate;

public sealed class ParentSeparateDetailService
{
    private readonly AppDbContext _db;
    private readonly TaskQueryService _queryService;
    public ParentSeparateDetailService(
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

            Console.WriteLine("----- HANDLE PARENT DETAIL START -----");
            Console.WriteLine(
                $"taskId={taskId} batchProductId={batchProductId} rootId={rootId}"
            );
            
            bool detailFinished = false;

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
                detailFinished = await CheckDetailFinished(conn, tx, rootId);
            }

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

            int productionModel = await _db.BatchProducts
                .Where(x => x.ID == batchProductId)
                .Join(
                    _db.ProductVersions,
                    bp => bp.Version_Id,
                    v => v.Id,
                    (bp, v) => v.ProductionModel
                )
                .FirstOrDefaultAsync();

        if (detailFinished)
        {
            var isInlinePainting = productionModel == 1;

                var nextStepType =
                    isInlinePainting
                        ? 3   // Finishing
                        : 2;  // Assembly

        Console.WriteLine(
            $"OPEN NEXT STAGE -> nextStepType={nextStepType} " +
            $"inline={isInlinePainting}"
        );

            await using (var openNext = conn.CreateCommand())
            {
                openNext.Transaction = tx;

                openNext.CommandText = @"
                UPDATE tasks t
                JOIN batches_products bp ON bp.ID = t.BatchProduct_ID
                JOIN toppartsteps ts ON ts.ID = t.TopPartStep_ID
                SET t.Tasks_Status = 1
                WHERE t.IsActive = 1
                AND ts.Step_Type = @stepType
                AND t.Tasks_Status = 5
                AND bp.ProductToPart_ID IS NULL
                AND COALESCE(bp.ParentBatchProduct_ID, bp.ID) = @rootId;";

                openNext.Parameters.Add(
                    new MySqlParameter("@rootId", rootId));

                openNext.Parameters.Add(
                    new MySqlParameter("@stepType", nextStepType));

                await openNext.ExecuteNonQueryAsync();
            }
        }

        Console.WriteLine("----- HANDLE PARENT DETAIL END -----");


        }

private async Task<bool> CheckDetailFinished(DbConnection conn, DbTransaction tx, int rootId)
{
   return await _queryService.IsDetailPhaseFinishedAll(conn, tx, rootId);
}

}