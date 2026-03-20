namespace ManaApp.Models
{
public class CompanyCalendarModel
{
    public DateTime WorkDate { get; set; }
    public TimeSpan? WorkStart { get; set; }
    public TimeSpan? WorkEnd { get; set; }
    public int? BreakMinutes { get; set; }
    public string? Notes { get; set; }
    public List<CompanyCalendarBreakModel> Breaks { get; set; } = new();
    public bool UseEmployeeDefaults { get; set; }
}

}