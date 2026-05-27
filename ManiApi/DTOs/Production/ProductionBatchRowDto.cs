namespace ManiApi.DTOs.Production;

public sealed class ProductionBatchRowDto
{
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = "";

    public int BatchProductId { get; set; }

    public int VersionId { get; set; }

    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string CategoryName { get; set; } = "";

    public int? ProductToPartId { get; set; }

    public int CategoryId { get; set; }
    public int? ParentCategoryId { get; set; }

    public string VersionName { get; set; } = "";

    public bool VersionIsActive { get; set; }

    public int ProductionModel { get; set; }

    public string? DetailName { get; set; }

    public string? HiddenDetailNames { get; set; }

    public bool IsPriority { get; set; }

    public int Planned { get; set; }

    public string? Comment { get; set; }

    public int Sold { get; set; }

    public int Done { get; set; }

    public int DetailsTotal { get; set; }

    public int DetailsChildTotal { get; set; }

    public int DetailsDone { get; set; }

    public int DetailsChildDone { get; set; }

    public DateTime? DetailStart { get; set; }

    public DateTime? DetailFinish { get; set; }

    public string? DetailFinishChildList { get; set; }

    public bool IsReadOnlyChild { get; set; }

    public bool IsCompleted { get; set; }

    public string DetailStatus { get; set; } = "";

    public string AssemblyStatus { get; set; } = "";
}