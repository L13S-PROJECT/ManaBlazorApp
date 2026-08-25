namespace ManaApp.Shared.DTOs.TopPart;

public sealed class TopPartSparePartProductOptionDto
{
    public int ProductTopPartId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";

    public int WorkflowId { get; set; }
    public int WorkflowVersion { get; set; }
    public DateTime? ReleasedDate { get; set; }
    public bool IsCurrent { get; set; }

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public bool IsSelected { get; set; }
}

public sealed class TopPartSparePartSelectionDto
{
    public int ProductTopPartId { get; set; }
    public int WorkflowId { get; set; }
}

