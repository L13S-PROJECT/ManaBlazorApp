using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("employees")]
    public class Employee
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("Employee_Name")]
        public string EmployeeName { get; set; } = "";

        [Column("UserName")]
        public string UserName { get; set; } = "";

        [Column("Password")]
        public string Password { get; set; } = "";

        [Column("Role")]
        public string Role { get; set; } = "";

        [Column("WorkCentrTypeID")]
        public int? WorkCentrTypeID { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}