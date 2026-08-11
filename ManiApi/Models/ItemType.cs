using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("itemtypes")]
    public class ItemType
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("TypeName")]
        public string TypeName { get; set; } = "";

        [Column("SortOrder")]
        public int SortOrder { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}