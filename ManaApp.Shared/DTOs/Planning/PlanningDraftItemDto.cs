namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningDraftItemDto
    {
        public uint DraftItemId { get; set; }

        public int TopPartId { get; set; }
        public byte TopPartType { get; set; }

        public string ProductName { get; set; } = "";

        public string ProductCode { get; set; } = "";

        public int WorkflowId { get; set; }

        public int WorkflowVersion { get; set; }

        public bool? IsWorkflowCurrent { get; set; }

        public int PlannedQty { get; set; }

        public int? ParentCategoryId { get; set; }

        public string ParentCategoryName { get; set; } = "";
    }
}
