using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("workflownodes")]
    public class WorkflowNode
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Workflow_ID")]
        public int WorkflowId { get; set; }

        [Column("ParentNode_ID")]
        public int? ParentNodeId { get; set; }

        [Column("NodeType")]
        public byte NodeType { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("TopPart_ID")]
        public uint? TopPartId { get; set; }

        [Column("ProductToPart_ID")]
        public int? ProductToPartId { get; set; }

        [Column("Output_ProductTopPart_ID")]
        public int? OutputProductToPartId { get; set; }

        [Column("WorkCenter_ID")]
        public int? WorkCenterId { get; set; }

        [Column("StepType_ID")]
        public uint? StepTypeId { get; set; }

        [Column("EstimatedMinutes")]
        public int? EstimatedMinutes { get; set; }

        [Column("Comments")]
        public string? Comments { get; set; }

        [Column("SortOrder")]
        public int SortOrder { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}