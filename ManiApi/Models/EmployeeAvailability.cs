using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("employee_availability")]
    public class EmployeeAvailability
    {
        [Key]
        public int ID { get; set; }

        public int EmployeeID { get; set; }

        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public string Status { get; set; } = "";

        public decimal? Hours { get; set; }

        public string? Notes { get; set; }
    }
}