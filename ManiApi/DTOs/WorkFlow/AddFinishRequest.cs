namespace ManiApi.DTOs.WorkFlow;

public class AddFinishRequest
{
    public int WorkflowId { get; set; }

    public int FlowOwnerNodeId { get; set; }
}