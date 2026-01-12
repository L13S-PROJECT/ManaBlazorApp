using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("producttopparts")]
    public class ProductTopPart
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("Version_ID")]
        public int VersionId { get; set; }

        [Column("TopPart_ID")]
        public int TopPartId { get; set; }

        
        [Column("Qty_Per_product")]
        public int QtyPerProduct { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
