namespace ManaApp.Models
{
    public class TaskDto
    {
        public int TaskId { get; set; }
        public int Status { get; set; } // 1,2,3,5
        public int StepType { get; set; } // 1=Detail
        public int EstimatedTotalMinutes { get; set; }
        public int EstimatedStartMinutes { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int BatchProductId { get; set; }
        public int ActualMinutes { get; set; }
        public int StepOrder { get; set; }
        public int? AssignedTo { get; set; }
        public int? WorkCenterId { get; set; }
        public int Capacity { get; set; }

    }
}