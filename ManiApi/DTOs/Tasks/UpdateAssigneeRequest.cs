namespace ManiApi.DTOs.Tasks
{
    public class UpdateAssigneeRequest
{
    public int TaskId { get; set; }
    public int? Assigned_To { get; set; }
}

}