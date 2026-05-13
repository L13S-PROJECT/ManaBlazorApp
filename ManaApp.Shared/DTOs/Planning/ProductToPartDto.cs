namespace ManaApp.Shared.DTOs.Planning;

public class ProductToPartDto
{
    public int Id { get; set; }
    public int TopPart_Id { get; set; }
    public string TopPart_Name { get; set; } = "";
    public bool IsSelected { get; set; }
    public int Qty { get; set; }
    public int OrderQty { get; set; }
    public int ProductOrderQty { get; set; }
    public string? RalCode { get; set; }
    public List<RalRowDto> ProductRalRows { get; set; } = new();
    public List<RalRowDto> PartRalRows { get; set; } = new();
}