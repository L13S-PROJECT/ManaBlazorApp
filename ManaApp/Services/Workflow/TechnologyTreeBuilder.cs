using ManaApp.Models;
using ManaApp.ViewModels.Workflow;

namespace ManaApp.Services.Workflow;

public class TechnologyTreeBuilder
{
    private readonly WorkflowState _state;

    public TechnologyTreeBuilder(WorkflowState state)
    {
        _state = state;
    }

    public List<TechnologyTreeItem> Build()
    {
        return new List<TechnologyTreeItem>();
    }

    

}