namespace ManiApi.Models
{
    public class ProductionBatchTopPart
    {
        public uint ID { get; set; }

        public uint Batch_ID { get; set; }
        public int TopPart_ID { get; set; }
        public int Workflow_ID { get; set; }

        public uint Planned_Qty { get; set; }
        public uint Done_Qty { get; set; }

        public bool IsPriority { get; set; }

        public string? Comments { get; set; }

        public bool IsActive { get; set; } = true;

        public ProductionBatch? Batch { get; set; }
        public TopPart? TopPart { get; set; }
        public Workflow? Workflow { get; set; }
    }
}