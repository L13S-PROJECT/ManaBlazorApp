using ManaApp.ViewModels.Workflow;

namespace ManaApp.Services.Workflow;

public class WorkflowStateService
{
    public WorkflowState State { get; } = new();
    public WorkflowState Current => State;
}