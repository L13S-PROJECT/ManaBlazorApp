namespace ManaApp.Shared.DTOs.Workflow;

public class DeleteWorkflowNodeRequest
{
    public int WorkflowId { get; set; }
    public int NodeId { get; set; }
}