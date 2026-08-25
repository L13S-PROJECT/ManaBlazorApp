using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("workflowcomponents")]
    public class WorkflowComponent
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Workflow_ID")]
        public int WorkflowId { get; set; }

        [Column("ComponentType")]
        public byte ComponentType { get; set; }

        [Column("TopPart_ID")]
        public uint? TopPartId { get; set; }

        [Column("Item_ID")]
        public int? ItemId { get; set; }

        [Column("ReferencedWorkflow_ID")]
        public int? ReferencedWorkflowId { get; set; }

        [Column("RequiredWorkflowNode_ID")]
        public int? RequiredWorkflowNodeId { get; set; }

        [Column("Quantity")]
        public decimal Quantity { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}