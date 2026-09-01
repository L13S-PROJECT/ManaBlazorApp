namespace ManaApp.Shared.DTOs.Production
{
    public class ProductionComponentStagingDto
    {
        public uint ProductionExecutionId { get; set; }

        public int TopPartId { get; set; }

        public string TopPartCode { get; set; } = "";

        public string TopPartName { get; set; } = "";

        public decimal RequiredQuantity { get; set; }

        public decimal StagedQuantity { get; set; }

        public decimal RemainingQuantity =>
            RequiredQuantity - StagedQuantity;

        public bool IsComplete =>
            StagedQuantity >= RequiredQuantity;
    }

    public class UpdateProductionComponentStagingRequest
    {
        public decimal StagedQuantity { get; set; }

        public int StagedByEmployeeId { get; set; }
    }
}