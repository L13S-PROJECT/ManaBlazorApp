namespace ManaApp.Shared.DTOs.TopPart
{
    public class TopPartGroupCategoryDto
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = "";
        public int? ParentId { get; set; }
    }
}