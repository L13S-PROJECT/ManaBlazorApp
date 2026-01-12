
using System.Text.Json.Serialization;

namespace ManaApp.Models;

public sealed class ProductRow
{
    public int id { get; set; }
    public string productCode { get; set; } = "";
    public string productName { get; set; } = "";
    public string categoryName { get; set; } = "";
    public string rootName { get; set; } = "";
    public string versionName { get; set; } = "";
    public string versionDate { get; set; } = "";

    [JsonPropertyName("Version_Id")]
    public int? VersionId { get; set; }

    public int InStock { get; set; }
    public int Planned { get; set; }
    public int DetailedInProgress { get; set; }
    public int DetailedFinish { get; set; }
    public int AssemblyINProgress { get; set; }
    public int AssemblyFinish { get; set; }
    public int FinishingInProgress { get; set; }
    public int FinishingAllocated { get; set; }
}

public sealed class CategoryRow
{
    public string CategoryName { get; set; } = "";
}

public sealed class ProductSimpleRow
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string CategoryName { get; set; } = "";
}

public sealed class ProductContentDto
{
    public int VersionId { get; set; }
    public string? VersionName { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
}

public sealed class StockSummary
{
    [JsonPropertyName("detailed")]        public int Detailed { get; set; }
    [JsonPropertyName("detailedFinish")]  public int DetailedFinish { get; set; }
    [JsonPropertyName("assembly")]        public int Assembly { get; set; }
    [JsonPropertyName("assemblyFinish")]  public int AssemblyFinish { get; set; }
    [JsonPropertyName("finishing")]       public int Finishing { get; set; }
    [JsonPropertyName("stock")]           public int Stock { get; set; }
    [JsonPropertyName("scrap")]           public int Scrap { get; set; }
    [JsonPropertyName("out")]             public int Out { get; set; }
    [JsonPropertyName("planned")]         public int Planned { get; set; }
}

public sealed class BatchPlannedRow
{
    [JsonPropertyName("versionId")]
    public int VersionId { get; set; }

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";

    [JsonPropertyName("productCode")]
    public string ProductCode { get; set; } = "";

    [JsonPropertyName("planned")]
    public int Planned { get; set; }

    [JsonPropertyName("detailedInProgress")]
    public int DetailedInProgress { get; set; }

    [JsonPropertyName("detailedFinish")]
    public int DetailedFinish { get; set; }

    [JsonPropertyName("assembly")]
    public int AssemblyInProgress { get; set; }

    [JsonPropertyName("done")]
    public int AssemblyFinish { get; set; }
}

public sealed class FinAllocatedDto
{
    [JsonPropertyName("finishingAllocated")]
    public int FinishingAllocated { get; set; }
}

public sealed class ProductGroupRow : IPlanningRow
{
    public bool IsCategory => false;

    public string CategoryName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";

    public int InStock { get; set; }
    public int Planned { get; set; }
    public int DetailedInProgress { get; set; }
    public int DetailedFinish { get; set; }
    public int AssemblyINProgress { get; set; }
    public int AssemblyFinish { get; set; }
    public int FinishingInProgress { get; set; }
    public int FinishingAllocated { get; set; }

    public List<ProductRow> Versions { get; set; } = new();

    public bool IsExpanded { get; set; }

}


public interface IPlanningRow
{
    bool IsCategory { get; }
}

public sealed class CategoryHeaderRow : IPlanningRow
{
    public bool IsCategory => true;
    public string CategoryName { get; set; } = "";
}
