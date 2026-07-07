namespace ManiApi.DTOs.Workflow;

public class WorkflowTopPartDto
{
    public int TopPartId { get; set; }

    public string TopPartName { get; set; } = "";

    public string TopPartCode { get; set; } = "";

    public byte Stage { get; set; }
}