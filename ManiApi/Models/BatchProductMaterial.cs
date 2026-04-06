using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    public class BatchProductMaterial
    {
        public int ID { get; set; }

        public int BatchProduct_ID { get; set; }
        public int SourceBatchProduct_ID { get; set; }

        public int Qty { get; set; }

        public DateTime Created_At { get; set; }

        public int? Task_ID { get; set; }

        public bool IsActive { get; set; }
    }
}