namespace ManiApi.Models
{
    public class ProductionReservation
    {
        public uint ID { get; set; }

        public uint ProductionRequirement_ID { get; set; }

        public int TopPart_ID { get; set; }

        public uint SourceMovement_ID { get; set; }

        public int? SourceWorkflow_ID { get; set; }

        public int? SourceWorkflowNode_ID { get; set; }

        public int ReservedQuantity { get; set; }

        public int ConsumedQuantity { get; set; }

        public int ReleasedQuantity { get; set; }

        public ProductionReservationStatus Status { get; set; }
            = ProductionReservationStatus.ACTIVE;

        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        public DateTime? Consumed_At { get; set; }

        public DateTime? Released_At { get; set; }

        public bool IsActive { get; set; } = true;

        public int RemainingQuantity =>
            ReservedQuantity - ConsumedQuantity - ReleasedQuantity;

        public ProductionRequirement? ProductionRequirement { get; set; }

        public TopPart? TopPart { get; set; }

        public StockMovementNew? SourceMovement { get; set; }

        public Workflow? SourceWorkflow { get; set; }

        public WorkflowNode? SourceWorkflowNode { get; set; }
    }
}