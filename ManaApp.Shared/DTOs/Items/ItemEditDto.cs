namespace ManaApp.Shared.DTOs.Items;

public class ItemEditDto
{
    public int Id { get; set; }

    public int ItemTypeId { get; set; }

    public string ItemCode { get; set; } = "";

    public string ItemName { get; set; } = "";

    public string? Description { get; set; }

    public string Unit { get; set; } = "";

    public bool IsActive { get; set; }
}