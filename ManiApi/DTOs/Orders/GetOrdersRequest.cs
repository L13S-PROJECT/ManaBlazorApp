namespace ManiApi.DTOs.Orders;

public class GetOrdersRequest
{
    public string? Search { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }
    public bool ShowArchived { get; set; }
}