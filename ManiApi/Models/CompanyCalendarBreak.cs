using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("company_calendar_breaks")]
    public class CompanyCalendarBreak
    {
        [Key]
        public int Id { get; set; }

        public DateTime WorkDate { get; set; }

        public TimeSpan BreakStart { get; set; }

        public TimeSpan BreakEnd { get; set; }

        public bool IsActive { get; set; }
    }
}