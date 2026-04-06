namespace ManiApi.Models
{
    public class BatchProductLink
    {
        public int ID { get; set; }

        public int ParentBatchProduct_ID { get; set; }
        public int ChildBatchProduct_ID { get; set; }

        public int Qty_Required { get; set; }

        public bool IsActive { get; set; }
    }
}