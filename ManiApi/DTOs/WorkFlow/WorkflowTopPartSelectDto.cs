namespace ManiApi.DTOs.WorkFlow;

public class WorkflowTopPartSelectDto
{
    public int TopPartId { get; set; }

    public string TopPartName { get; set; } = "";

    public string TopPartCode { get; set; } = "";

    public bool Disabled { get; set; }
}