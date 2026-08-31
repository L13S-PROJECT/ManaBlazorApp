namespace ManaApp.Shared.DTOs.Tasks
{
    public class TaskNewListItemDto
    {
        public uint Id { get; set; }

        public uint ProductionExecutionId { get; set; }

        public int WorkflowNodeId { get; set; }

        public string ProcessName { get; set; } = "";

        public int? EmployeeId { get; set; }

        public string EmployeeName { get; set; } = "";

        public int WorkCenterId { get; set; }

        public string WorkCenterName { get; set; } = "";

        public int Quantity { get; set; }

        public int Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? PausedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}