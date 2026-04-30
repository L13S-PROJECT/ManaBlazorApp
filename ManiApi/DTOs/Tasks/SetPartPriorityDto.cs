namespace ManiApi.DTOs.Tasks
{
    public sealed class SetPartPriorityDto
{
    public int BatchProductId { get; set; }
    public int ProductToPartId { get; set; }
    public bool Tasks_Priority { get; set; }
}

}