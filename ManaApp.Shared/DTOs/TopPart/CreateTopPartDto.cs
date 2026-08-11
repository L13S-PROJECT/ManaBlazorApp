namespace ManaApp.Shared.DTOs.TopPart
{
    public class CreateTopPartDto
    {
        public string TopPartName { get; set; } = "";
        public string TopPartCode { get; set; } = "";
        public byte? TopPartType { get; set; }
        public int? TopPartCategoryID { get; set; }
        public string? Description { get; set; }
    }
}