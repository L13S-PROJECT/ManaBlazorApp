namespace ManaApp.Models
{
    public class CompanyCalendarBreakModel
    {
        public DateTime WorkDate { get; set; }

        public TimeSpan BreakStart { get; set; }

        public TimeSpan BreakEnd { get; set; }

        public bool IsActive { get; set; }
    }
}