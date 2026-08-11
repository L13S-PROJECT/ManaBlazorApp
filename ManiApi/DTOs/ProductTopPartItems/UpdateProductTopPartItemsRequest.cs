namespace ManiApi.DTOs.ProductTopPartItems
{
    public class UpdateProductTopPartItemsRequest
    {
        public List<Row> Rows { get; set; } = new();

        public class Row
        {
            public int Id { get; set; }

            public decimal Qty { get; set; }

            public int SortOrder { get; set; }
        }
    }
}