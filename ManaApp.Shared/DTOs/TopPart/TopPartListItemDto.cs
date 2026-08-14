namespace ManaApp.Shared.DTOs.TopPart
{
    public class TopPartListItemDto
    {
        public int Id { get; set; }
        public string TopPartName { get; set; } = "";
        public string TopPartCode { get; set; } = "";
        public byte TopPartType { get; set; }
        public int? TopPartCategoryID { get; set; }
        public string? Description { get; set; }
        public DateTime? DraftCreatedDate { get; set; }
        public int? ReleasedVersion { get; set; }
        public DateTime? ReleasedDate { get; set; }
        public string? FlowComment { get; set; }
    }
}