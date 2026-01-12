using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("categories")]
    public class Category
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("category_name")]
        public string CategoryName { get; set; } = string.Empty;

        [Column("parent_ID")]
        public int? ParentId { get; set; }

 
        public bool IsActive { get; set; }

    }
}
