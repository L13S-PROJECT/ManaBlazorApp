using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("toppart")]
    public class TopPart
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("TopPart_Name")]
        public string TopPartName { get; set; } = "";

        [Column("TopPart_Code")]
        public string TopPartCode { get; set; } = "";

        [Column("Stage")]
        public byte Stage { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
