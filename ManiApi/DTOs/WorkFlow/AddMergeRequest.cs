namespace ManiApi.DTOs.WorkFlow;

public class AddMergeRequest
{
    public int WorkflowId { get; set; }

    // Flow Owner, kuram tiek veidots jaunais MERGE Flow
    public int CurrentFlowId { get; set; }

    public List<int> MergeFlowIds { get; set; } = new();
    
}