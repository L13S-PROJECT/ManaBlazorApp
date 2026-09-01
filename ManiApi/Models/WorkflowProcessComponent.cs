using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("workflowprocesscomponents")]
    public class WorkflowProcessComponent
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("ProcessNode_ID")]
        public int ProcessNodeId { get; set; }

        [Column("WorkflowComponent_ID")]
        public int WorkflowComponentId { get; set; }

        [Column("Quantity")]
        public decimal Quantity { get; set; }

        [Column("RequiresStaging")]
        public bool RequiresStaging { get; set; } = true;

        public WorkflowComponent? WorkflowComponent { get; set; }

    }
}
