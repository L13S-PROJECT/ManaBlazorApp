namespace ManiApi.DTOs.WorkFlow;

public class SaveNodeCommentsRequest
{
    public int NodeId { get; set; }

    public string? Comments { get; set; }
}