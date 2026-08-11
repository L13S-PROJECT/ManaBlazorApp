using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("producttoppartitems")]
    public class ProductTopPartItem
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("ProductTopPart_ID")]
        public uint ProductTopPartId { get; set; }

        [Column("Item_ID")]
        public int ItemId { get; set; }

        [Column("Qty")]
        public decimal Qty { get; set; }

        [Column("AreaM2")]
        public decimal? AreaM2 { get; set; }

        [Column("LengthM")]
        public decimal? LengthM { get; set; }

        [Column("SortOrder")]
        public int SortOrder { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}