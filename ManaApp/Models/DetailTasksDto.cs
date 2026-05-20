namespace ManaApp.Models;

public sealed class DetailTasksDto
{
    public List<DetailTasksPartDto> Parts { get; set; } = new();
}

public sealed class DetailTasksPartDto
{
    public int ProductToPartId { get; set; }
    public string? TopPartName { get; set; }

    public int Qty { get; set; }
    public string QtyDisplay { get; set; } = "";

    public string Indicator { get; set; } = "gray";
    public bool IsActivated { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public List<DetailTasksStepDto> Steps { get; set; } = new();
    public bool IsEditable { get; set; }
}

public sealed class DetailTasksStepDto
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