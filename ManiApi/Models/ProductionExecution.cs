namespace ManiApi.Models
{
    public class ProductionExecution
    {
        public uint ID { get; set; }

        public uint? ProductionBatchTopPart_ID { get; set; }

        public uint? ProductionRequirement_ID { get; set; }

        public int TopPart_ID { get; set; }

        public int Workflow_ID { get; set; }

        public int Quantity { get; set; }

        public ProductionExecutionStatus Status { get; set; }
            = ProductionExecutionStatus.WAITING;

        public DateTime? Started_At { get; set; }

        public DateTime? Completed_At { get; set; }

        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public ProductionBatchTopPart? ProductionBatchTopPart { get; set; }

        public TopPart? TopPart { get; set; }

        public Workflow? Workflow { get; set; }
        public ProductionRequirement? ProductionRequirement { get; set; }
    }
}