namespace ManiApi.DTOs.Tasks
{
    public class SetTaskPushDto
{
    public int BatchProductId { get; set; }
    public int ProductToPartId { get; set; }
    public bool Tasks_Push { get; set; }
}
}