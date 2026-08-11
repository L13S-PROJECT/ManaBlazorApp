namespace ManiApi.DTOs.WorkFlow;

public class SaveTopPartWorkflowDraftRequest
{
    public int SourceWorkflowId { get; set; }

    public List<SaveTopPartWorkflowNodeRequest> Nodes { get; set; } = new();
    public List<SaveTopPartWorkflowConnectionRequest> Connections { get; set; } = new();
    public List<SaveTopPartWorkflowComponentRequest> Components { get; set; } = new();
}