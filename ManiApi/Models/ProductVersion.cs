using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("versions")]
    public class ProductVersion
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("version_name")]
        public string VersionName { get; set; } = string.Empty;

        [Column("version_rasejums")]
        public string VersionRasejums { get; set; } = string.Empty;

        [Column("version_date")]
        public DateOnly VersionDate { get; set; }

        [Column("version_comment")]
        public string VersionComment { get; set; } = string.Empty;

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
