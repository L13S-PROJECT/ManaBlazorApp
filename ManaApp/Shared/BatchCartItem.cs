namespace ManaApp.Shared
{
    public class BatchCartItem
    {
        public int ProductId { get; set; }
        public int VersionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int Qty { get; set; }
        public string? Comment { get; set; }

        public BatchCartItem Clone() => new()
        {
            ProductId = ProductId,
            VersionId = VersionId,
            Name = Name,
            Code = Code,
            Qty = Qty,
            Comment = Comment
        };
    }
}
