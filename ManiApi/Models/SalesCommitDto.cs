namespace ManiApi.Models;

public sealed class SalesCommitDto
{
    public List<SalesCommitItemDto> Items { get; set; } = new();
}

public sealed class SalesCommitItemDto
{
    public int VersionId { get; set; }
    public int Qty { get; set; }
}
