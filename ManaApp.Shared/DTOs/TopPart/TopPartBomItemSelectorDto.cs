namespace ManaApp.Shared.DTOs.TopPart
{
    public class TopPartBomItemSelectorDto
    {
        public int ItemId { get; set; }

        public string ItemCode { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string Unit { get; set; } = "";

        public bool IsSelected { get; set; }
        public decimal Quantity { get; set; }

        public bool CanEdit { get; set; }
    }
}