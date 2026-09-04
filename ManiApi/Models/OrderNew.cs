using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("orders_new")]
    public class OrderNew
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Order_Number")]
        public string OrderNumber { get; set; } = "";

        [Column("Order_Date")]
        public DateTime? OrderDate { get; set; }

        [Column("Customer_Name")]
        public string CustomerName { get; set; } = "";

        [Column("Created_At")]
        public DateTime CreatedAt { get; set; }

        [Column("Comment")]
        public string? Comment { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
