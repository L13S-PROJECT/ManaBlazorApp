using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("company_calendar_breaks")]
    public class CompanyCalendarBreak
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }


        [Column("WorkDate")]
        public DateTime WorkDate { get; set; }

        [Column("BreakStart")]
        public TimeSpan BreakStart { get; set; }

        [Column("BreakEnd")]
        public TimeSpan BreakEnd { get; set; }
        
        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}