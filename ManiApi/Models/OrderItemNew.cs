using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("order_items_new")]
    public class OrderItemNew
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Order_ID")]
        public int OrderId { get; set; }

        [Column("Customer_Code")]
        public string CustomerCode { get; set; } = "";

        [Column("Name")]
        public string Name { get; set; } = "";

        [Column("Quantity")]
        public int Quantity { get; set; }

        [Column("TopPart_ID")]
        public int TopPartId { get; set; }

        [Column("Workflow_ID")]
        public int WorkflowId { get; set; }

        [Column("Ral_Color_ID")]
        public int? RalColorId { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}