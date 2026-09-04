namespace ManaApp.Shared.DTOs.Orders;

public class SaveCustomerCodeMapNewDto
{
    public int OrderDraftItemId { get; set; }

    public string CustomerName { get; set; } = "";
    public string CustomerCode { get; set; } = "";

    public int TopPartId { get; set; }
    public int WorkflowId { get; set; }
    public int? RalColorId { get; set; }
}