using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("toppart_sparepart")]
    public class TopPartSparePart
    {
        [Key]
        [Column("ID")]
        public uint Id { get; set; }

        [Column("ProductTopPart_ID")]
        public uint ProductTopPartId { get; set; }

        [Column("SparePartTopPart_ID")]
        public uint SparePartTopPartId { get; set; }

        [Column("Workflow_ID")]
        public int WorkflowId { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
