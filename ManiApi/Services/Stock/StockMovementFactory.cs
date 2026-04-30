using ManiApi.Models;

namespace ManiApi.Services.Stock
{
    public static class StockMovementFactory
    {
        public static StockMovement CreateAssemblyMovement(
            int versionId,
            int batchProductId,
            int taskId,
            int qty,
            int? ralColorId)
        {
            return new StockMovement
            {
                Version_ID = versionId,
                BatchProduct_ID = batchProductId,
                RAL_Color_ID = ralColorId,
                Move_Type = MoveType.ASSEMBLY,
                Stock_Qty = -qty,
                Created_At = DateTime.UtcNow,
                Task_ID = taskId,
                IsActive = true
            };
        }

        public static StockMovement CreateFinishingMovement(
            int versionId,
            int batchProductId,
            int taskId,
            int qty,
            int? ralColorId)
        {
            return new StockMovement
            {
                Version_ID = versionId,
                BatchProduct_ID = batchProductId,
                RAL_Color_ID = ralColorId,
                Move_Type = MoveType.FINISHING,
                Stock_Qty = qty,
                Created_At = DateTime.UtcNow,
                Task_ID = taskId,
                IsActive = true
            };
        }
    }
}