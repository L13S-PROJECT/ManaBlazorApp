namespace ManaApp.Shared.DTOs.TopPart;

public class TopPartWorkflowNodeOptionDto
{
    public int Id { get; set; }
    public byte NodeType { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}