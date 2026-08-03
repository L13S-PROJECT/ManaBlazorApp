
using ManaApp.Shared.DTOs.Workflow;

namespace ManaApp.ViewModels.Workflow;

public class WorkflowGraphItem
{
    public WorkflowNodeModel? Node { get; set; }

    public AvailableFlowDto? Flow { get; set; }
    public List<WorkflowGraphItem> FlowNodes { get; set; } = new();
    public WorkflowPartModel? Part { get; set; }
    public List<WorkflowGraphItem> NextNodes { get; set; } = new();
    public List<WorkflowGraphItem> PreviousNodes { get; set; } = new();
    public List<WorkflowGraphItem> Children { get; set; } = new();
    public int Depth { get; set; }
    public int Level { get; set; }
    public int Branch { get; set; }
    public bool IsRoot { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsSelected { get; set; }
    public bool IsDependency { get; set; }
    public int ExplorerDepth { get; set; }
    public bool HasValidationError { get; set; }
    public bool IsSubPart { get; set; }
    public bool HasProcessValidationError { get; set; }
}