namespace ManiApi.DTOs.WorkFlow;

public class SaveWorkflowPartsRequest
{
    public int VersionId { get; set; }

    public List<SaveWorkflowPartItem> Parts { get; set; } = new();
}