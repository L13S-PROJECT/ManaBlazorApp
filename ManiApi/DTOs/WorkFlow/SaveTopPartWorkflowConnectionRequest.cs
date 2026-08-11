namespace ManiApi.DTOs.WorkFlow;

public class SaveTopPartWorkflowConnectionRequest
{
    public int FromNodeId { get; set; }

    public int ToNodeId { get; set; }
}