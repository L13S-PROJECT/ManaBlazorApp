using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;


namespace ManiApi.Models
{
    [Table("toppart")]
    public class TopPart
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("TopPart_Name")]
        public string TopPartName { get; set; } = "";

        [Column("TopPart_Code")]
        public string TopPartCode { get; set; } = "";

        [Column("Stage")]
        public byte Stage { get; set; }
        [Column("TopPartType")]
        public byte TopPartType { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
