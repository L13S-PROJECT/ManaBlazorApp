namespace ManaApp.Shared.DTOs.Planning;

public sealed class PlanningGroupDto
{
    public string CategoryName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";

    public int OrderQty { get; set; }

    public int InStock { get; set; }

    public int Planned { get; set; }

    public int DetailedInProgress { get; set; }

    public int DetailedFinish { get; set; }

    public int AssemblyINProgress { get; set; }

    public int AssemblyFinish { get; set; }

    public int FinishingInProgress { get; set; }

    public List<ProductRowDto> Versions { get; set; } = new();
    public bool IsExpanded { get; set; }
}

public sealed class ProductRowDto
{
    public int? VersionId { get; set; }

    public string? productName { get; set; }

    public string? productCode { get; set; }

    public string? versionName { get; set; }

    public string? categoryName { get; set; }

    public string? rootName { get; set; }

    public int Planned { get; set; }

    public int DetailedInProgress { get; set; }

    public int DetailedFinish { get; set; }

    public int AssemblyINProgress { get; set; }

    public int AssemblyFinish { get; set; }

    public int FinishingInProgress { get; set; }

    public int InStock { get; set; }

    public bool IsRalRow { get; set; }

    public string? RalCode { get; set; }

    public int OrderQty { get; set; }
}
