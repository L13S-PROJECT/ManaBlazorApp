using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("workcentr_type")]
    public class WorkCenter
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("WorkCentr_Name")]
        public string WorkCentr_Name { get; set; } = "";

        [Column("WorkCentr_Code")]
        public string WorkCentr_Code { get; set; } = "";   // ← pievieno

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
