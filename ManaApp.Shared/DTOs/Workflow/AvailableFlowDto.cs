namespace ManaApp.Shared.DTOs.Workflow;

public class AvailableFlowDto
{
    public int FinishNodeId { get; set; }

    public AvailableFlowType FlowType { get; set; }

    public string OwnerName { get; set; } = "";
    public string DisplayName { get; set; } = "";

    public bool IsConsumed { get; set; }

    public int StartNodeId { get; set; }
    public int? OwnerProductToPartId { get; set; }
    public bool IsSelectable { get; set; }
}

public enum AvailableFlowType
{
    Unknown = 0,
    TopPart = 1,
    SubPart = 2,
    Merge = 3
}

