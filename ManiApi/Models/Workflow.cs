using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("workflows")]
    public class Workflow
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Version_ID")]
        public int VersionId { get; set; }

        [Column("ParentNode_ID")]
        public int? ParentNodeId { get; set; }

        [Column("Workflow_Name")]
        public string Name { get; set; } = string.Empty;    

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}