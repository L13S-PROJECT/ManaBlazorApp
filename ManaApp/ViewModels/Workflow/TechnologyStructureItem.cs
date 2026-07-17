using ManaApp.Models;
using ManaApp.DTOs.Workflow;

namespace ManaApp.ViewModels.Workflow;

public class TechnologyStructureItem
{
    public WorkflowPartModel? Part { get; set; }

    public WorkflowNodeModel? Node { get; set; }
    public AvailableFlowDto? Flow { get; set; }

    public int FlowLevel { get; set; }
    public List<TechnologyStructureItem> Children { get; set; } = new();

    public List<TechnologyStructureItem> FlowChildren { get; set; } = new();

    public List<TechnologyStructureItem> PartChildren { get; set; } = new();

    public TechnologyStructureItem? Parent { get; set; }
    
    public bool IsSelected { get; set; }

    public bool IsExpanded { get; set; } = true;
    public bool HasValidationError { get; set; }
}