namespace ManaApp.Models;


public sealed class ProductBatchRow
{
    public int BatchId { get; set; }
    public int BatchProductId { get; set; }

    public string BatchCode { get; set; } = "";

    public int Planned { get; set; }

    public int Sold { get; set; }

    public int Done { get; set; }

    public string? Comment { get; set; }
    public DateTime? StartedAt { get; set; }

    public int VersionId { get; set; }
    public string? VersionName { get; set; }

    public string ProductCode { get; set; } = "";
public string CategoryName { get; set; } = "";
}

