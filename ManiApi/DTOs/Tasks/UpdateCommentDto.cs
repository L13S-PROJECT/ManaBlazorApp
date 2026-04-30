namespace ManiApi.DTOs.Tasks
{
    public sealed class UpdateCommentDto
{
    public int TaskId { get; set; }
    public string? Comment { get; set; }
    public bool IsForEmployee { get; set; }

}
}