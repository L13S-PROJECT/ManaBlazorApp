namespace ManiApi.Models
{
    public class StockMovementNew
    {
        public uint ID { get; set; }

        public int TopPart_ID { get; set; }

        public uint? ProductionBatchTopPart_ID { get; set; }

        public int? WorkflowNode_ID { get; set; }

        public int? RAL_Color_ID { get; set; }

        public string Movement_Type { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public uint? SourceMovement_ID { get; set; }

        public uint? ConsumedByBatch_ID { get; set; }

        public DateTime Created_At { get; set; }

        public bool IsActive { get; set; } = true;

        public TopPart? TopPart { get; set; }

        public ProductionBatchTopPart? ProductionBatchTopPart { get; set; }

        public WorkflowNode? WorkflowNode { get; set; }

        public RalColor? RalColor { get; set; }

        public StockMovementNew? SourceMovement { get; set; }

        public ProductionBatch? ConsumedByBatch { get; set; }

    }
}