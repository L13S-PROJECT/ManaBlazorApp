namespace ManiApi.DTOs.WorkFlow;

public class AddMergeRequest
{
    public int WorkflowId { get; set; }

    // Aktīvās plūsmas FINISH
    public int ActiveFinishNodeId { get; set; }

    // Pārējie FINISH, kurus apvienojam
    public List<int> FinishNodeIds { get; set; } = new();
}