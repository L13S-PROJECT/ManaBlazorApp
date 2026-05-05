namespace ManiApi.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }

        public string CustomerCode { get; set; } = "";
        public string Name { get; set; } = "";
        public int Quantity { get; set; }

        public int? VersionId { get; set; }
        public int? ProductToPartId { get; set; }
        public int? RalColorId { get; set; }

        public bool IsActive { get; set; }
    }
}