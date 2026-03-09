namespace ManaApp.Models;

public class WorkCenterGroup
{
    public int? WorkCenterId { get; set; }
    public string? WorkCenterName { get; set; }
    public List<EmployeeGroup> Employees { get; set; } = new();
}