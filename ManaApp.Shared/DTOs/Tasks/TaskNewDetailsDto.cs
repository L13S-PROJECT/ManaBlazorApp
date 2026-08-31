namespace ManaApp.Shared.DTOs.Tasks
{
    public class TaskNewDetailsDto : TaskNewListItemDto
    {
        public int TopPartId { get; set; }

        public string TopPartCode { get; set; } = "";

        public string TopPartName { get; set; } = "";

        public int WorkflowId { get; set; }

        public int WorkflowVersion { get; set; }

        public List<TaskNewStatusHistoryDto> StatusHistory { get; set; } = [];
    }
}