namespace ManiApi.DTOs.WorkFlow;

public class CreateWorkflowRequest
{
    public int VersionId { get; set; }

    public int? TopPartId { get; set; }

    public string WorkflowName { get; set; } = "";
}