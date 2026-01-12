using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("toppartsteps")]
    public class TopPartStep
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("ProductToPart_ID")]
        public int ProductToPartId { get; set; }

        [Column("Step_Order")]
        public int StepOrder { get; set; }

        [Column("Step_Name")]
        public string StepName { get; set; } = "";

        [Column("Step_Type")]
        public int StepType { get; set; }   // FK uz step_type.ID

        [Column("Parallel_Group")]
        public int ParallelGroup { get; set; }

    [NotMapped]
public int? DependsOnStepId { get; set; }

    
        [Column("IsFinal")]
        public bool IsFinal { get; set; }

        [Column("IsMandatory")]
        public bool IsMandatory { get; set; }

        [Column("WorkCentr_ID")]
        public int WorkCentrId { get; set; }  // FK uz workcentr_type.ID

        [Column("Comments")]
        public string? Comments { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
