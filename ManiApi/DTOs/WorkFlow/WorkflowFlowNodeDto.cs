using ManaApp.Shared.DTOs.Workflow;

namespace ManiApi.DTOs.WorkFlow;

public class WorkflowFlowNodeDto
{
    public int FinishNodeId { get; set; }

    public AvailableFlowType FlowType { get; set; }

    public List<WorkflowFlowNodeDto> Children { get; set; } = [];
}