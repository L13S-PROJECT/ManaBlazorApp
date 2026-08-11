namespace ManaApp.Shared.DTOs.Items
{
    public class ProductTopPartItemDto
    {
        public int Id { get; set; }

        public int ItemId { get; set; }

        public string ItemCode { get; set; } = "";

        public string ItemName { get; set; } = "";

        public string Unit { get; set; } = "";

        public decimal Qty { get; set; }

        public int SortOrder { get; set; }

        public int ItemTypeId { get; set; }

        public string ItemTypeName { get; set; } = "";
    }
}