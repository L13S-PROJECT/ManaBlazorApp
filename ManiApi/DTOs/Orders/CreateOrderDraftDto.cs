using ManiApi.Services.Pdf;

namespace ManiApi.DTOs.Orders;

public class CreateOrderDraftDto
{
    public PdfService.OrderHeader Header { get; set; } = new();

    public List<PdfService.OrderItem> Items { get; set; } = new();
}