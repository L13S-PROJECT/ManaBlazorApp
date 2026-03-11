using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models;

[Table("ral_colors")]
public class RalColor
{
    public int ID { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
}