namespace ManaApp.Models;


public sealed class ProductBatchRow
{
    public int BatchId { get; set; }
    public int BatchProductId { get; set; }
    public string BatchCode { get; set; } = "";
   public string? Comment { get; set; }
    public DateTime? StartedAt { get; set; }
    public string ProductCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int VersionId { get; set; }
    public bool VersionIsActive { get; set; }
    public string? VersionName { get; set; }
    public bool IsReadOnlyChild { get; set; }
    public int Planned { get; set; }
    public string? HiddenDetailNames { get; set; }
    public int Sold { get; set; }

    public int Done { get; set; }
    public int DetailsChildTotal { get; set; }
    public int DetailedInProgress { get; set; }
    public int DetailedFinish { get; set; }
    public int AssemblyInProgress { get; set; }
    public int AssemblyFinish { get; set; }
    public int FinishingInProgress { get; set; }
    public int DetailedStarted { get; set; }
    public int DetailsTotal { get; set; }

    public DateTime? DetailStart { get; set; }
    public DateTime? DetailFinish { get; set; }
    public DateTime? AssemblyStart { get; set; }
    public DateTime? AssemblyFinishDate { get; set; }
    public string? DetailFinishChildList { get; set; }
    public int FinStatus1 { get; set; }
    public int FinStatus2 { get; set; } 
    public int FinStatus3 { get; set; } 
    public int DetailsDone { get; set; }
    public int DetailsChildDone { get; set; }

    public int? ProductToPartId { get; set; }

    public int CategoryId { get; set; }

    public int? ParentCategoryId { get; set; }
    public bool IsProduct => ProductToPartId == null;
    public bool IsCompleted { get; set; }
    public string DetailStatus { get; set; } = "";
    public string AssemblyStatus { get; set; } = "";
    public string? DetailName { get; set; }

    public int ProductionModel { get; set; }

    public string AssemblyCssClass { get; set; } = "";
    public DateTime? AssemblyDisplayDate { get; set; }

}

