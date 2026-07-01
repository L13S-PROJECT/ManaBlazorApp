namespace ManiApi.DTOs.WorkFlow;

public class CreateWorkflowConnectionRequest
{
    public int FromNodeId { get; set; }

    public int ToNodeId { get; set; }
}