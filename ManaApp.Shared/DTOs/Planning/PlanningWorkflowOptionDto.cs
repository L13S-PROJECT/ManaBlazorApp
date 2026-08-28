namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningWorkflowOptionDto
    {
        public int WorkflowId { get; set; }

        public int WorkflowVersion { get; set; }

        public string Name { get; set; } = "";

        public bool IsCurrent { get; set; }
    }
}