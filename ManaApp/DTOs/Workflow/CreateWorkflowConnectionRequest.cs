namespace ManaApp.DTOs.Workflow;

public class CreateWorkflowConnectionRequest
{
    public int FromNodeId { get; set; }

    public int ToNodeId { get; set; }
}