namespace ManiApi.DTOs.Tasks
{
    public sealed class OpenFinishingDto
{
    public int BatchProductId { get; set; }
    public int ProductToPartId { get; set; }
    public int Qty { get; set; }
    public int? RalColorId { get; set; }
    public string? Comment { get; set; }
}
}