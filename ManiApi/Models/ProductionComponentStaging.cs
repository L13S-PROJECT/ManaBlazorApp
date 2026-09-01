namespace ManiApi.Models
{
    public class ProductionComponentStaging
    {
        public uint ID { get; set; }

        public uint ProductionExecution_ID { get; set; }

        public int WorkflowProcessComponent_ID { get; set; }

        public decimal RequiredQuantity { get; set; }

        public decimal StagedQuantity { get; set; }

        public int? StagedByEmployee_ID { get; set; }

        public DateTime? Staged_At { get; set; }

        public bool IsActive { get; set; } = true;

        public ProductionExecution? ProductionExecution { get; set; }

        public WorkflowProcessComponent? WorkflowProcessComponent { get; set; }

        public Employee? StagedByEmployee { get; set; }
    }
}