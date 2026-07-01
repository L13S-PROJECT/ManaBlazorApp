using ManiApi.Models;

namespace ManiApi.Services.Tasks
{
    public static class TaskFactory
    {
        public static ManiApi.Models.Tasks CreateFinishingTask(
            int batchProductId,
            int finishingStepId,
            int qty,
            int? ralColorId,
            string? comment,
            int? sourceProductToPartId)
        {
            return new ManiApi.Models.Tasks
            {
                BatchProduct_ID = batchProductId,
                TopPartStep_ID  = finishingStepId,
                Tasks_Status    = 1,
                IsActive        = true,
                Qty_Done        = qty,
                Qty_Scrap       = 0,
                RAL_Color_ID    = ralColorId,
                Tasks_Comment   = comment,
                Source_ProductToPart_ID =
                    sourceProductToPartId > 0
                        ? sourceProductToPartId
                        : null,
            };
        }
    }
}