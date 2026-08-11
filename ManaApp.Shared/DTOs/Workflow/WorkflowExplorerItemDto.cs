namespace ManaApp.Shared.DTOs.Workflow;

public class WorkflowExplorerItemDto
{
    public string Name { get; set; } = "";
    public int WorkflowNodeId { get; set; }
    public List<WorkflowExplorerNodeDto> Nodes { get; set; } = new();
    public List<WorkflowExplorerItemDto> Children { get; set; } = new();
    public List<int> ParentFlowIds { get; set; } = new();
    public List<int> ChildFlowIds { get; set; } = new();
    public List<int> MergeParentFlowIds { get; set; } = new();
    public AvailableFlowType FlowType { get; set; }
    public int Level { get; set; }
}