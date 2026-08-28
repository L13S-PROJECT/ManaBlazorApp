namespace ManaApp.Shared.DTOs.Planning
{
    public class SavePlanningDraftItemRequest
    {
        public int TopPartId { get; set; }

        public int WorkflowId { get; set; }

        public int PlannedQty { get; set; }
    }
}