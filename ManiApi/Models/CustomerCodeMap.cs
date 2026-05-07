namespace ManiApi.Models
{
    public class CustomerCodeMap
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = "";
        public string CustomerCode { get; set; } = "";
        public int? VersionId { get; set; }
        public int? ProductToPartId { get; set; }
        public int? TopPartId { get; set; }
        public int? RalColorId { get; set; }
        public bool IsProduct { get; set; }
        public bool IsPart { get; set; }

    }
}
