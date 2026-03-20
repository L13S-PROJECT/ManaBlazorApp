using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("company_calendar")]
    public class CompanyCalendar
    {
        [Key]
        [Column("WorkDate")]
        public DateTime WorkDate { get; set; }

        [Column("WorkStart")]
        public TimeSpan? WorkStart { get; set; }

        [Column("WorkEnd")]
        public TimeSpan? WorkEnd { get; set; }

        [Column("BreakMinutes")]
        public int? BreakMinutes { get; set; }

        [Column("Notes")]
        public string? Notes { get; set; }

        [NotMapped]
        public List<CompanyCalendarBreak> Breaks { get; set; } = new();
        
        [Column("UseEmployeeDefaults")]
        public bool UseEmployeeDefaults { get; set; }
    }
}