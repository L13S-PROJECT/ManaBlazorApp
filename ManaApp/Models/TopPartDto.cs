namespace ManaApp.Models;

public class TopPartDto
{
    public int Id { get; set; }
    public string TopPartName { get; set; } = "";
    public string TopPartCode { get; set; } = "";
    public byte Stage { get; set; }
    public string Display => $"{TopPartCode} — {TopPartName}";
}

public class StageStepMapDto
{
    public int Stage { get; set; }
    public int StepTypeId { get; set; }
}