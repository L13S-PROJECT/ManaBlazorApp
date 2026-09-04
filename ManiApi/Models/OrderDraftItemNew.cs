using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("order_draft_items_new")]
    public class OrderDraftItemNew
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Order_Draft_ID")]
        public int OrderDraftId { get; set; }

        [Column("Customer_Code")]
        public string CustomerCode { get; set; } = "";

        [Column("Name")]
        public string Name { get; set; } = "";

        [Column("Quantity")]
        public int Quantity { get; set; }

        [Column("TopPart_ID")]
        public int? TopPartId { get; set; }

        [Column("Workflow_ID")]
        public int? WorkflowId { get; set; }

        [Column("Ral_Color_ID")]
        public int? RalColorId { get; set; }

        [Column("Is_Mapped")]
        public bool IsMapped { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}