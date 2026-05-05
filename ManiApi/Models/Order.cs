namespace ManiApi.Models
{
    public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime? OrderDate { get; set; }
    public string CustomerName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

}