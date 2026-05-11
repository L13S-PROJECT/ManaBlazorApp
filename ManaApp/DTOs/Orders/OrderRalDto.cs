namespace ManaApp.Models.DTOs;

public sealed class OrderRalDto
{
    public int VersionId { get; set; }
    public int? RalColorId { get; set; }
    public string? RalCode { get; set; }
    public int Qty { get; set; }
}