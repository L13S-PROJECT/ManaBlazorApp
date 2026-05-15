namespace ManaApp.Models.Orders;

public class MappingListItem
{
    public int ParentCategoryId { get; set; }

    public string ParentCategoryName { get; set; } = "";
    public string ProductCode { get; set; } = "";

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = "";

    public int ProductId { get; set; }

    public string ProductName { get; set; } = "";

    public int VersionId { get; set; }

    public string VersionName { get; set; } = "";

    public bool VersionIsActive { get; set; }
}