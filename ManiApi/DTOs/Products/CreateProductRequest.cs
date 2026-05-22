namespace ManiApi.DTOs.Products
{
    public class CreateProductRequest
    {
        public string ProductName { get; set; } = "";
        public string ProductCode { get; set; } = "";
        public int CategoryId { get; set; }

        public string? VersionName { get; set; }

        public int ProductionModel { get; set; } = 0;

        public string? VersionRasejums { get; set; }
        public string? VersionDate { get; set; }
        public string? VersionComment { get; set; }
    }
}