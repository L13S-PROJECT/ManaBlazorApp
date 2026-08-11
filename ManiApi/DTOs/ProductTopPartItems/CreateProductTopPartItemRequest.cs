namespace ManiApi.DTOs.ProductTopPartItems
{
    public class CreateProductTopPartItemRequest
    {
        public int ProductTopPartId { get; set; }

        public int ItemId { get; set; }

        public decimal Qty { get; set; }
    }
}