namespace ManaApp.Shared.DTOs.Orders;

    public class CreateOrderDraftNewDto
    {
        public OrderDraftHeaderNewDto Header { get; set; } = new();
        public List<OrderDraftImportItemNewDto> Items { get; set; } = [];
    }

    public class OrderDraftHeaderNewDto
    {
        public string OrderNumber { get; set; } = "";
        public string Customer { get; set; } = "";
        public string? Date { get; set; }
    }

    public class OrderDraftImportItemNewDto
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
    }
