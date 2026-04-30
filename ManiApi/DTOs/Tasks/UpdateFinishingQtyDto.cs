namespace ManiApi.DTOs.Tasks
{
   public sealed class UpdateFinishingQtyDto
{
    public int TaskId { get; set; }
    public int Qty { get; set; }
    public string? Comment { get; set; }
} 
}