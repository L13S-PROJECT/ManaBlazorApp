namespace ManiApi.Models
{
    public class DetailTasksDto
    {
        public List<DetailPartDto> Parts { get; set; } = new();
    }

    public class DetailPartDto
    {
        public int ProductToPartId { get; set; }
        public string? TopPartName { get; set; }

        public int Qty { get; set; }              // 🔥 jau aprēķināts
        public string QtyDisplay { get; set; } = ""; // 🔥 "10+5" u.c.

        public string Indicator { get; set; } = "gray";

        public bool IsActivated { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public List<DetailStepDto> Steps { get; set; } = new();

        public bool IsEditable { get; set; }
    }

    public class DetailStepDto
    {
        public int StepId { get; set; }
        public string? StepName { get; set; }
        public int TaskId { get; set; }
        public int? AssignedTo { get; set; }
        public int? ClaimedBy { get; set; }

        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public string? Comment { get; set; }
        public bool IsCommentForEmployee { get; set; }

        public int Status { get; set; }
    }
}