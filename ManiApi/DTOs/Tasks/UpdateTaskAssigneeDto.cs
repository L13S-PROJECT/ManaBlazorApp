namespace ManiApi.DTOs.Tasks
{
   public sealed class UpdateTaskAssigneeDto
{
    public int TaskId { get; set; }
    public int? Assigned_To { get; set; } // null = noņemt
} 
}