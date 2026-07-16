namespace ManiApi.DTOs.WorkFlow;

public class SaveQtyPerProductRequest
{
    public int ProductToPartId { get; set; }

    public int QtyPerProduct { get; set; }
}