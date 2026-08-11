namespace ManiApi.DTOs.WorkFlow;

public class SaveTopPartWorkflowComponentRequest
{
    public int Id { get; set; }

    public byte ComponentType { get; set; }

    public uint? TopPartId { get; set; }

    public int? ItemId { get; set; }

    public int? ReferencedWorkflowId { get; set; }

    public decimal Quantity { get; set; }
}