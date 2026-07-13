namespace ManiApi.DTOs.WorkFlow;

public class AddTopPartRequest
{
    public int VersionId { get; set; }
    public int TopPartId { get; set; }
    public int? ParentProductTopPartId { get; set; }
    public int? AttachToNodeId { get; set; }
    
}