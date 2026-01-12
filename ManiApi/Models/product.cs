using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("products")]
    public class Product
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("product_name")]
        public string ProductName { get; set; } = string.Empty;

        [Column("product_code")]
        public string ProductCode { get; set; } = string.Empty;

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
