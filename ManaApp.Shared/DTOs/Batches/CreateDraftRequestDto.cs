namespace ManaApp.Shared.DTOs.Batches;

public sealed class CreateDraftRequestDto
{
    public int? BatchId { get; set; }
    public string Title { get; set; } = "";
    public string? Comment { get; set; }

    public List<CreateDraftItemDto> Items { get; set; } = new();
}