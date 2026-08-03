using ManiApi.Models;
using ManaApp.Shared.DTOs.Workflow;

namespace ManiApi.DTOs.WorkFlow;

public class FlowInfoDto
{
    public WorkflowNode? StartNode { get; set; }
    public WorkflowNode? FinishNode { get; set; }
    public int? OwnerProductToPartId { get; set; }
    public AvailableFlowType FlowType { get; set; }
    public bool IsConsumed { get; set; }
    public bool IsFinished { get; set; }

    public List<WorkflowExplorerItemDto> Explorer { get; set; } = new();
}