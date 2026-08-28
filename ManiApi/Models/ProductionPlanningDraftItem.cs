namespace ManiApi.Models
{
    public class ProductionPlanningDraftItem
    {
        public uint ID { get; set; }

        public uint Draft_ID { get; set; }

        public int TopPart_ID { get; set; }

        public int Workflow_ID { get; set; }

        public int Planned_Qty { get; set; }

        public DateTime Created_At { get; set; }

        public bool IsActive { get; set; }

        public ProductionPlanningDraft Draft { get; set; } = null!;

        public TopPart TopPart { get; set; } = null!;
        public Workflow Workflow { get; set; } = null!;
        
    }
}