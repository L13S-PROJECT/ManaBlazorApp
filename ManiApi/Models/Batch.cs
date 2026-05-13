using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models;

[Table("batches")]
public class Batch
{
    public int ID { get; set; }

    public string Batches_Code { get; set; } = "";

    public int Batches_Statuss { get; set; }

    public bool IsActive { get; set; }
}