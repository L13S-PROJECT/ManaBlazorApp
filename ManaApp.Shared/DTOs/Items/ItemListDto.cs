namespace ManaApp.Shared.DTOs.Items
{
    public class ItemListDto
    {
        public int Id { get; set; }

        public int ItemTypeId { get; set; }

        public string ItemTypeName { get; set; } = "";

        public string ItemCode { get; set; } = "";

        public string ItemName { get; set; } = "";

        public string Unit { get; set; } = "";
    }
}
