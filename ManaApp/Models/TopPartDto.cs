namespace ManaApp.Models;

public class TopPartDto
{
    public int Id { get; set; }
    public string TopPartName { get; set; } = "";
    public string TopPartCode { get; set; } = "";
    public string Display => $"{TopPartCode} — {TopPartName}";
}