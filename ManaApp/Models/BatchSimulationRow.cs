namespace ManaApp.Models
{
public class BatchSimulationRow
{
    public int BatchId { get; set; }
    public string BatchCode { get; set; } = "";
    public int BatchProductId { get; set; }
    public int VersionId { get; set; }

    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string VersionName { get; set; } = "";

    public bool IsPriority { get; set; }

    public int Planned { get; set; }
    public string? Comment { get; set; }

    public int DetailedInProgress { get; set; }
    public int DetailedFinish { get; set; }

    public int AssemblyInProgress { get; set; }
public int AssemblyFinish { get; set; }

public int FinishingInProgress { get; set; }
public int FinStatus1 { get; set; }
public int FinStatus2 { get; set; }
public int FinStatus3 { get; set; }

public DateTime? DetailStart { get; set; }
public DateTime? DetailFinishDate { get; set; }

public DateTime? AssemblyStart { get; set; }
public DateTime? AssemblyFinishDate { get; set; }

public int DetailedStarted { get; set; }
public int DetailsTotal { get; set; }

public int Priority { get; set; }

}

}