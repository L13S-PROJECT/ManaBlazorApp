namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningCorrectionBatchOptionDto
    {
        public uint BatchId { get; set; }

        public string BatchCode { get; set; } = "";

        public DateTime? StartDate { get; set; }

        public int PlannedQty { get; set; }

        public int DoneQty { get; set; }
    }
}