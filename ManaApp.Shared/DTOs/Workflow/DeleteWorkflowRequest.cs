namespace ManaApp.Shared.DTOs.Workflow;

public class DeleteWorkflowRequest
{
    public int WorkflowId { get; set; }
    public int NodeId { get; set; }
}