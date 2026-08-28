namespace ManiApi.Models
{
    public class ProductionPlanningDraft
    {
        public uint ID { get; set; }

        public DateOnly? Plan_Date { get; set; }

        public string? Batch_Code { get; set; }

        public byte Draft_Type { get; set; }

        public uint? Source_Batch_ID { get; set; }

        public ProductionPlanningDraftStatus Status { get; set; }
    = ProductionPlanningDraftStatus.Draft;

        public string? Comments { get; set; }

        public DateTime Created_At { get; set; }

        public DateTime? Approved_At { get; set; }

        public bool IsActive { get; set; }

        public ProductionBatch? SourceBatch { get; set; }
    }
}