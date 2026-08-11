using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("toppartcategory")]
    public class TopPartCategory
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Category_Name")]
        public string CategoryName { get; set; } = "";

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}