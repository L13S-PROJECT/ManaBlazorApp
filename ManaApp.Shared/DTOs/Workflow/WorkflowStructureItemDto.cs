namespace ManaApp.Shared.DTOs.Workflow;

public class WorkflowStructureItemDto
{
    public WorkflowNodeDto? Node { get; set; }

    public int FlowLevel { get; set; }

    public bool HasValidationError { get; set; }

    public List<WorkflowStructureItemDto> Children { get; set; } = [];
}