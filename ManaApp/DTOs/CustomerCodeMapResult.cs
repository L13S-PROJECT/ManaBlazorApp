namespace ManaApp.DTOs.Orders
{

public class CustomerCodeMapResult
{
    public string CustomerCode { get; set; } = "";

    public int? VersionId { get; set; }

    public int? ProductToPartId { get; set; }

    public int? RalColorId { get; set; }

    public bool IsProduct { get; set; }

    public bool IsPart { get; set; }

    public bool IsMapped { get; set; }
}

}