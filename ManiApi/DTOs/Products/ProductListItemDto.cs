namespace ManiApi.DTOs.Products
{
    public class ProductListItemDto
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string? CategoryName { get; set; }
    public string? RootName { get; set; }
    public int VersionId { get; set; }
    public string? VersionName { get; set; }
    public DateOnly? VersionDate { get; set; }
    public bool VersionIsActive { get; set; }
    public bool IsPriority { get; set; }

    public int GroupType { get; set; }  
    public bool IsActive { get; set; } 
}
}
