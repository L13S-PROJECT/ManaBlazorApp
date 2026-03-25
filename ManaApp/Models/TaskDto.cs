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
        public int ProductToPartId { get; set; }
        public bool IsFinal { get; set; }
        public bool IsPriority { get; set; }
        public int Priority { get; set; }
        public bool TasksPriority { get; set; }
        public bool TasksPush { get; set; }
        public int PriorityLevel { get; set; }
        public bool BatchPriority { get; set; }
        public int BatchOrder { get; set; }
        public int QtyDone { get; set; }

    }
}