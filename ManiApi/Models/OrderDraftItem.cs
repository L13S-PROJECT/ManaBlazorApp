using System.ComponentModel.DataAnnotations.Schema;
namespace ManiApi.Models
{
    public class OrderDraftItem
    {
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

        [Column("Version_ID")]
        public int? VersionId { get; set; }

        [Column("Product_To_Part_ID")]
        public int? ProductToPartId { get; set; }

        [Column("Ral_Color_ID")]    
        public int? RalColorId { get; set; }

        [Column("Is_Mapped")]
        public bool IsMapped { get; set; }

        [Column("Is_Active")]
        public bool IsActive { get; set; }
    }
}