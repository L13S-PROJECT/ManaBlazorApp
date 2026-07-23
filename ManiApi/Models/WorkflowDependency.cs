using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("workflowdependencies")]
    public class WorkflowDependency
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Workflow_ID")]
        public int WorkflowId { get; set; }

        [Column("Node_ID")]
        public int NodeId { get; set; }

        [Column("DependsOnNode_ID")]
        public int DependsOnNodeId { get; set; }
    }
}