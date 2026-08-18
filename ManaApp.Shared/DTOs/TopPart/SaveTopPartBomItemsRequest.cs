namespace ManaApp.Shared.DTOs.TopPart
{
    public class SaveTopPartBomItemsRequest
    {
        public List<SaveTopPartBomItemDto> Items { get; set; } = new();
    }

    public class SaveTopPartBomItemDto
    {
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
    }
}