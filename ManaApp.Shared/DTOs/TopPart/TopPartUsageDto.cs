namespace ManaApp.Shared.DTOs.TopPart
{
    public class TopPartUsageDto
    {
        public int Count { get; set; }

        public List<TopPartUsageItemDto> Items { get; set; } = new();
    }

    public class TopPartUsageItemDto
    {
        public int TopPartId { get; set; }

        public string TopPartCode { get; set; } = string.Empty;

        public string TopPartName { get; set; } = string.Empty;

        public byte TopPartType { get; set; }
    }
}