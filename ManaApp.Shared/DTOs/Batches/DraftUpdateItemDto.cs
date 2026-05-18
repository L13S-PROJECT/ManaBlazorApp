
namespace ManaApp.Shared.DTOs.Batches;

public sealed class DraftUpdateItemDto

    {
        public int? ItemId { get; set; }
        public int VersionId { get; set; }
        public int? ProductToPartId { get; set; }
        public bool VersionIsActive { get; set; } = true;

        public int Qty { get; set; }

        public string? Comment { get; set; }
    }