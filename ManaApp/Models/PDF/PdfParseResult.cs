namespace ManaApp.Models.Pdf;

public class PdfParseResult
{
    public OrderHeader Header { get; set; } = new();
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderHeader
{
    public string OrderNumber { get; set; } = "";
    public string Date { get; set; } = "";
    public string Customer { get; set; } = "";
}

public class OrderItem
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
}