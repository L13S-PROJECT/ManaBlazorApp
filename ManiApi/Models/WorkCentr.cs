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
    public string WorkCentr_Code { get; set; } = "";

    [Column("WorkCenter_Order")]
    public int WorkCenter_Order { get; set; }

    [Column("Step_Type_ID")]
    public int? Step_Type_ID { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; }
}
}
