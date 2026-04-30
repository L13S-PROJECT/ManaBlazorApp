namespace ManiApi.DTOs.Tasks
{
    public sealed class UpdateCommentVisibilityDto
{
    public int TaskId { get; set; }
    public bool IsCommentForEmployee { get; set; }
}
}