using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("step_type")]
    public class StepType
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("StepType_Name")]
        public string StepTypeName { get; set; } = "";

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
