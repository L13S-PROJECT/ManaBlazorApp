namespace ManaApp.Shared.DTOs.TopPart;

public class TopPartBomPartOptionDto
{
    public int TopPartId { get; set; }
    public string TopPartCode { get; set; } = string.Empty;
    public string TopPartName { get; set; } = string.Empty;

    public int? ReleasedWorkflowId { get; set; }
    public int? ReleasedWorkflowVersion { get; set; }

    public bool HasDraft { get; set; }
    public bool CanAdd { get; set; }
}