namespace ManiApi.DTOs.Planning;

public sealed class PlanningProductDto
{
    public string CategoryName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";

    public int InStock { get; set; }
    public int OrderQty { get; set; }

    public int Planned { get; set; }
    public int DetailedInProgress { get; set; }
    public int DetailedFinish { get; set; }

    public int AssemblyINProgress { get; set; }
    public int AssemblyFinish { get; set; }

    public int FinishingInProgress { get; set; }

    public List<PlanningVersionDto> Versions { get; set; } = new();
}

public sealed class PlanningVersionDto
{
    public int VersionId { get; set; }

    public string VersionName { get; set; } = "";

    public int Planned { get; set; }
    public int DetailedInProgress { get; set; }
    public int DetailedFinish { get; set; }

    public int AssemblyINProgress { get; set; }
    public int AssemblyFinish { get; set; }

    public List<PlanningRalDto> Rals { get; set; } = new();
}

public sealed class PlanningRalDto
{
    public int VersionId { get; set; }

    public int? RalColorId { get; set; }

    public string RalCode { get; set; } = "";

    public int InStock { get; set; }

    public int OrderQty { get; set; }

    public int FinishingInProgress { get; set; }

    public int Qty { get; set; }
}
