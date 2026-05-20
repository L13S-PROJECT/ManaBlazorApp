namespace ManaApp.DTOs.Assembly;

public sealed class AssemblySummaryDto
{
    public int ProductToPartId { get; set; }

    public string TopPartName { get; set; } = "";

    public int Qty { get; set; }

    public string QtyDisplay { get; set; } = "";

    public string Indicator { get; set; } = "gray";

    public string StatusText { get; set; } = "";
}