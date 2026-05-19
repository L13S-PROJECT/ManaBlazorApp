namespace ManaApp.Shared.DTOs.Batches;

public class BatchCartItem
    {
        public int ProductId { get; set; }
        public int VersionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? VersionName { get; set; }
        public string Code { get; set; } = string.Empty;
        public int Qty { get; set; }
        public string? Comment { get; set; }
        public int? CategoryId { get; set; }
        public int? ProductToPartId { get; set; }
        public int? ParentCategoryId { get; set; }
        public bool IsExpanded { get; set; } = true;
        public bool IsArchivedVersion { get; set; }
        public BatchCartItem Clone() => new()
            {
                ProductId = ProductId,
                VersionId = VersionId,
                Name = Name,
                VersionName = VersionName,
                Code = Code,
                Qty = Qty,
                Comment = Comment,
                CategoryId = CategoryId,
                ParentCategoryId = ParentCategoryId,

                // 👇 SVARĪGI
                ProductToPartId = ProductToPartId,
                IsExpanded = IsExpanded,
                IsArchivedVersion = IsArchivedVersion
            };
    }