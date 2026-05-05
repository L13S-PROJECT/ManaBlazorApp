namespace ManiApi.DTOs.Tasks
{
public class EmployeeLoadDto
{
    public string? EmployeeName { get; set; }
    public string? WorkCenterName { get; set; }
    public int? WorkCentrTypeID { get; set; }

    public List<TaskItemDto> InProgress { get; set; } = new();
    public List<TaskItemDto> Priority { get; set; } = new();
    public List<TaskItemDto> Normal { get; set; } = new();
}
}