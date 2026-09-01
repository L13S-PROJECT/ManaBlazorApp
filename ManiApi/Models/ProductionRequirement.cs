namespace ManiApi.Models
{
    public class ProductionRequirement
    {
        public uint ID { get; set; }

        public ProductionRequirementSourceType SourceType { get; set; }

        public uint? ProductionPlanningDraftItem_ID { get; set; }

        public uint? ProductionBatchTopPart_ID { get; set; }

        public int SourceTopPart_ID { get; set; }

        public int RequiredTopPart_ID { get; set; }

        public uint? ParentRequirement_ID { get; set; }

        public int WorkflowProcessComponent_ID { get; set; }

        public int GrossQuantity { get; set; }

        public int StockCoveredQuantity { get; set; }

        public int IncomingCoveredQuantity { get; set; }

        public int NetQuantity { get; set; }

        public int Priority { get; set; }

        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public ProductionPlanningDraftItem? ProductionPlanningDraftItem { get; set; }

        public ProductionBatchTopPart? ProductionBatchTopPart { get; set; }

        public TopPart? SourceTopPart { get; set; }

        public TopPart? RequiredTopPart { get; set; }

        public ProductionRequirement? ParentRequirement { get; set; }

        public ICollection<ProductionRequirement> ChildRequirements { get; set; }
            = new List<ProductionRequirement>();

        public WorkflowProcessComponent? WorkflowProcessComponent { get; set; }

    }
}