namespace ManiApi.DTOs.WorkFlow;

public class AddMergeRequest
{
    public int WorkflowId { get; set; }

    // Flow Owner, kuram tiek veidots jaunais MERGE Flow
    public int CurrentFinishNodeId { get; set; }

    public List<int> MergeFinishNodeIds { get; set; } = new();
    
}