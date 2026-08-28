namespace ManaApp.Shared.DTOs.Planning
{
    public class UpdatePlanningCorrectionBatchRequest
    {
        public string BatchCode { get; set; } = "";

        public List<UpdatePlanningCorrectionBatchItemRequest> Items { get; set; }
            = [];
    }

    public class UpdatePlanningCorrectionBatchItemRequest
    {
        public uint BatchTopPartId { get; set; }

        public int TopPartId { get; set; }

        public int WorkflowId { get; set; }

        public int PlannedQty { get; set; }
    }
}