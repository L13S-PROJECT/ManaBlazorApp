//FinishingTasksService.cs

using ManiApi.Data;
using Microsoft.EntityFrameworkCore;
using ManiApi.Models;

namespace ManiApi.Services.Finishing
{
    public class FinishingTasksService
    {
        private readonly AppDbContext _db;

        public FinishingTasksService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<(bool isPainting, int availableQty)> GetChildFinishingData(
    int batchProductId,
    int productToPartId)
{
    // 1) pārbaudām IsPainting (Detailed solis!)
    var isPainting = await _db.TopPartSteps
        .AnyAsync(x =>
            x.IsActive &&
            x.ProductToPartId == productToPartId &&
            x.StepType == 1 &&
            x.IsPainting == true);

    // 2) Assembly stock (child batchProduct līmenī)
    var assemblyStock = await _db.StockMovements
        .Where(x =>
            x.IsActive &&
            x.BatchProduct_ID == batchProductId &&
            x.Move_Type == MoveType.ASSEMBLY)
        .SumAsync(x => (int?)x.Stock_Qty) ?? 0;

    var available = Math.Max(assemblyStock, 0);

    return (isPainting, available);
}

    }

}
