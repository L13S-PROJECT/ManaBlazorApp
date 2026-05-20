namespace ManiApi.DTOs.Assembly;

public sealed class AssemblySummaryDto
{
    public int ProductToPartId { get; set; }

    public string TopPartName { get; set; } = "";

    public int Qty { get; set; }

    public string QtyDisplay { get; set; } = "";

    // gray / orange / yellow / green
    public string Indicator { get; set; } = "gray";

    // NotStarted / Waiting / InProgress / Done
    public string StatusText { get; set; } = "";
}