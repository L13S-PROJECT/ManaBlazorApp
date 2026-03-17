using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("employee_work_log")]
    public class EmployeeWorkLog
    {
        public int ID { get; set; }

        public int EmployeeID { get; set; }

        public DateTime WorkDate { get; set; }

        public TimeSpan? TimeFrom { get; set; }

        public TimeSpan? TimeTo { get; set; }

        public decimal? Hours { get; set; }

        public string? Notes { get; set; }
    }
}