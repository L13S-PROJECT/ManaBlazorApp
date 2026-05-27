using ManiApi.Data;
using ManiApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ManiApi.Services.ProductionFlows.ParentInline;

public sealed class InlineParentAssemblyService
{
    private readonly AppDbContext _db;

    public InlineParentAssemblyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task FinishAssemblyTask(
        int assemblyTaskId)
    {
        var task = await _db.Tasks
                .FirstOrDefaultAsync(x =>
                    x.ID == assemblyTaskId &&
                    x.IsActive);

            if (task is null)
                throw new Exception("Assembly task not found.");
        
        var batchProductId = task.BatchProduct_ID;
        var qty = task.Qty_Done;
        var ralColorId = task.RAL_Color_ID;

            if (qty <= 0)
                throw new Exception("Assembly qty invalid.");
        
        task.Tasks_Status = 3;
        task.Finished_At = DateTime.UtcNow;

        if (task.Qty_Done <= 0)
            {
                task.IsActive = false;
            }

        _db.Tasks.Update(task);

        await _db.SaveChangesAsync();

        var versionId = await _db.BatchProducts
            .Where(x => x.ID == batchProductId)
            .Select(x => x.Version_Id)
            .FirstAsync();

        _db.StockMovements.Add(new StockMovement
        {
            Version_ID = versionId,
            BatchProduct_ID = batchProductId,

            Move_Type = MoveType.ASSEMBLY,
            Stock_Qty = -qty,

            RAL_Color_ID = ralColorId,

            Task_ID = assemblyTaskId,
            IsActive = true
        });

        _db.StockMovements.Add(new StockMovement
        {
            Version_ID = versionId,
            BatchProduct_ID = batchProductId,

            Move_Type = MoveType.STOCK,
            Stock_Qty = qty,

            RAL_Color_ID = ralColorId,

            Task_ID = assemblyTaskId,
            IsActive = true
        });

        await _db.SaveChangesAsync();


    }
}