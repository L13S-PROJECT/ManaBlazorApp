using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("workflownodeconnections")]
    public class WorkflowNodeConnection
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("FromNode_ID")]
        public int FromNodeId { get; set; }

        [Column("ToNode_ID")]
        public int ToNodeId { get; set; }
    }
}