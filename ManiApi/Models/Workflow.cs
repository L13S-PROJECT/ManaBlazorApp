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

        [Column("TopPart_ID")]
        public uint? TopPartId { get; set; }

        [Column("WorkflowVersion")]
        public int WorkflowVersion { get; set; }

        [Column("ParentWorkflow_ID")]
        public int? ParentWorkflowId { get; set; }

        [Column("Version_ID")]
        public int? VersionId { get; set; }

        [Column("ParentNode_ID")]
        public int? ParentNodeId { get; set; }

        [Column("Workflow_Name")]
        public string Name { get; set; } = string.Empty;    

        [Column("IsCurrent")]
        public bool IsCurrent { get; set; }

        [Column("Status")]
        public WorkflowStatus Status { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }

    public enum WorkflowStatus
        {
            Draft = 1,
            Released = 2
        }
}