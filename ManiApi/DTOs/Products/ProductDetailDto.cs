namespace ManiApi.DTOs.Products
{
    public class ProductDetailDto
    {
        public int TopPartId { get; set; }
        public string TopPartName { get; set; } = "";
        public string TopPartCode { get; set; } = "";
        public int Stage { get; set; }
        public int Quantity { get; set; }
        public int ProductToPartId { get; set; }
    }
}