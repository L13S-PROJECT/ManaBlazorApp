
namespace ManaApp.Shared.DTOs.Batches;

public sealed class DraftUpdateRequestDto
    {
        public int? BatchId { get; set; }

        public List<DraftUpdateItemDto> Items { get; set; } = new();
        public string Title { get; set; } = "";

        public string? Comment { get; set; }
    }