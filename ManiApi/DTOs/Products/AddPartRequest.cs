namespace ManiApi.DTOs.Products
{
    public sealed class AddPartRequest
        {
            public int productId { get; set; }      // Produkta Id
            public int versionId { get; set; }
            public int topPartId { get; set; }      // Detaļas Id
            public int qtyPerProduct { get; set; }  // Vesels skaitlis >=1
        }

}