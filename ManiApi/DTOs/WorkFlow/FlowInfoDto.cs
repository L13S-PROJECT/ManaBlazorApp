using ManiApi.Models;

namespace ManiApi.DTOs.WorkFlow;

public class FlowInfoDto
{
    public WorkflowNode? StartNode { get; set; }
    public WorkflowNode? FinishNode { get; set; }
    public int? OwnerProductToPartId { get; set; }
    public AvailableFlowType FlowType { get; set; }
    public bool IsConsumed { get; set; }
    public bool IsFinished { get; set; }
}