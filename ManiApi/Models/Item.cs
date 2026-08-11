using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("items")]
    public class Item
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("ItemType_ID")]
        public int ItemTypeId { get; set; }

        [Column("ItemCode")]
        public string ItemCode { get; set; } = "";

        [Column("ItemName")]
        public string ItemName { get; set; } = "";

        [Column("Description")]
        public string? Description { get; set; }

        [Column("Unit")]
        public string Unit { get; set; } = "";

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}