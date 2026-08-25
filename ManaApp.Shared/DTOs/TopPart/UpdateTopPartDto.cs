namespace ManaApp.Shared.DTOs.TopPart
{
    public class UpdateTopPartDto
    {
        public int Id { get; set; }

        public string TopPartName { get; set; } = "";

        public string TopPartCode { get; set; } = "";

        public int? TopPartCategoryID { get; set; }

        public string? Description { get; set; }

        public List<TopPartSparePartSelectionDto> Selections { get; set; } = [];
    }
}