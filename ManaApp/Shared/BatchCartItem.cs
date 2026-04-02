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
        public int? CategoryId { get; set; }
        public int? ProductToPartId { get; set; }
        public int? ParentCategoryId { get; set; }
        public BatchCartItem Clone() => new()
        {
            ProductId = ProductId,
            VersionId = VersionId,
            Name = Name,
            Code = Code,
            Qty = Qty,
            Comment = Comment,
            CategoryId = CategoryId,
            ParentCategoryId = ParentCategoryId
        };
    }
}
