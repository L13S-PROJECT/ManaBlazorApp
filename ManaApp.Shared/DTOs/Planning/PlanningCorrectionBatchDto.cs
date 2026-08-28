namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningCorrectionBatchDto
        {
            public uint BatchId { get; set; }

            public string BatchCode { get; set; } = "";

            public DateTime? StartDate { get; set; }

            public List<PlanningCorrectionBatchItemDto> Items { get; set; } = [];
        }

    public class PlanningCorrectionBatchItemDto : PlanningDraftItemDto
        {
            public uint BatchTopPartId { get; set; }

            public int DoneQty { get; set; }

            public bool CanEdit { get; set; }
        }
}