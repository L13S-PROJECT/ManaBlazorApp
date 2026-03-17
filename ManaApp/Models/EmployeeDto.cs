namespace ManaApp.Models
{
    public class EmployeeDto
        {
            public int Id { get; set; }

            public string Name { get; set; } = "";
            public TimeSpan? WorkStart { get; set; }
            public TimeSpan? WorkEnd { get; set; }
            public decimal DefaultDailyHours { get; set; }
            public string Role { get; set; } = "";

            public int? WorkCentrTypeID { get; set; }
        }
}