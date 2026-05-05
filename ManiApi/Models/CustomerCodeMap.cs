namespace ManiApi.Models
{
    public class CustomerCodeMap
    {
        public int Id { get; set; }

        public string CustomerName { get; set; } = "";
        public string CustomerCode { get; set; } = "";

        public int? VersionId { get; set; }
        public int? ProductToPartId { get; set; }
    }
}
