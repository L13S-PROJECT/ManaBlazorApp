namespace ManiApi.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }

        public string CustomerCode { get; set; } = "";
        public string Name { get; set; } = "";
        public int Quantity { get; set; }

        public bool IsActive { get; set; }
        public int? CustomerCodeMapId { get; set; }
        public int? VersionId { get; set; }
    }
}