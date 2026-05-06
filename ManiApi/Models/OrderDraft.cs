using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    public class OrderDraft
    {
        public int Id { get; set; }
        [Column("Order_Number")]
        public string OrderNumber { get; set; } = "";
        [Column("Order_Date")]
        public DateTime? OrderDate { get; set; }

        [Column("Customer_Name")]
        public string CustomerName { get; set; } = "";

        [Column("Created_At")]
        public DateTime CreatedAt { get; set; }

        [NotMapped]
        public bool IsCompleted { get; set; }
    }
}