namespace ManaApp.Models.Orders
{
public class OrderDraftItemViewModel
{
    public string CustomerCode { get; set; } = "";

    public string Name { get; set; } = "";

    public int Quantity { get; set; }

    public int? VersionId { get; set; }

    public int? TopPartId { get; set; }

    public int? ProductToPartId { get; set; }

    public int? RalColorId { get; set; }

    public bool IsMapped { get; set; }

    public string? ProductName { get; set; }

    public string? VersionName { get; set; }

    public string? RalColorName { get; set; }

    public string? TopPartName { get; set; }

    public string? MappingType { get; set; }

    public bool IsProduct { get; set; }

    public bool IsPart { get; set; }
    public bool VersionIsActive { get; set; }

    public int? CustomerCodeMapId { get; set; }

    public int Id { get; set; }
    
}
}