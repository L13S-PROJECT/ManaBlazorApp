using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models;

[Table("batches_products")]
public class BatchProduct
{
    public int ID { get; set; }

    public int Batch_Id { get; set; }

    public int Version_Id { get; set; }

    public int Planned_Qty { get; set; }

    public int Done_Qty { get; set; }

    public bool IsActive { get; set; }

    public bool is_priority { get; set; }
}
