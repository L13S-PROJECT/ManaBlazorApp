namespace ManaApp.Models;

public class EmployeeGroup
{
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public List<GanttRow> Tasks { get; set; } = new();
}