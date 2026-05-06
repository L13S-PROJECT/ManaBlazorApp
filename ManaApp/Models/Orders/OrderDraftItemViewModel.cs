namespace ManaApp.Models.Orders
{
public class OrderDraftItemViewModel
{
    public string CustomerCode { get; set; } = "";

    public string Name { get; set; } = "";

    public int Quantity { get; set; }

    public int? VersionId { get; set; }

    public int? ProductToPartId { get; set; }

    public int? RalColorId { get; set; }

    public bool IsMapped { get; set; }

    
}
}