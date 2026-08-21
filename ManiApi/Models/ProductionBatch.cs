namespace ManiApi.Models
{
    public class ProductionBatch
    {
        public uint ID { get; set; }

        public string Batch_Code { get; set; } = string.Empty;

        public sbyte Batch_Status { get; set; } = 1;

        public DateTime? Start_Date { get; set; }
        public DateTime? End_Date { get; set; }

        public string? Comments { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime Created_At { get; set; }
    }
}