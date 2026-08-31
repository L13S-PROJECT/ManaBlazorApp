namespace ManiApi.Models
{
    public class TaskNew
    {
        public uint ID { get; set; }

        public uint ProductionExecution_ID { get; set; }

        public int WorkflowNode_ID { get; set; }

        public int? Employee_ID { get; set; }

        public int WorkCenter_ID { get; set; }

        public int Quantity { get; set; }

        public TaskNewStatus Status { get; set; }
            = TaskNewStatus.WAITING;

        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        public DateTime? Assigned_At { get; set; }

        public DateTime? Started_At { get; set; }

        public DateTime? Paused_At { get; set; }

        public DateTime? Completed_At { get; set; }

        public bool IsActive { get; set; } = true;

        public ProductionExecution? ProductionExecution { get; set; }

        public WorkflowNode? WorkflowNode { get; set; }

        public Employee? Employee { get; set; }

        public WorkCenter? WorkCenter { get; set; }
    }
}