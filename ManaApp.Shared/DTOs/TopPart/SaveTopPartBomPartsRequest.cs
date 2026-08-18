namespace ManaApp.Shared.DTOs.TopPart
{
    public class SaveTopPartBomPartsRequest
    {
        public List<SaveTopPartBomPartDto> Parts { get; set; } = new();
    }

    public class SaveTopPartBomPartDto
    {
        public int TopPartId { get; set; }
        public decimal Quantity { get; set; }
    }
}