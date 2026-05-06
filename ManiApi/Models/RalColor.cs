using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models;

[Table("ral_colors")]
public class RalColor
{
    [Column("ID")]
    public int ID { get; set; }

    [Column("Name")]
    public string Name { get; set; } = "";

    [Column("IsActive")]
    public bool IsActive { get; set; }
}