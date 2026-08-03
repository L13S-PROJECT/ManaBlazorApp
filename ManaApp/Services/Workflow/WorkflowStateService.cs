using ManaApp.ViewModels.Workflow;
using ManaApp.DTOs.Workflow;

namespace ManaApp.Services.Workflow;

public class WorkflowStateService
{
    public WorkflowState State { get; } = new();
    public WorkflowState Current => State;
    
    public void Clear()
        {
            State.Workflow = null;
            State.Graph.Clear();
            State.PartNodeByProductToPartId.Clear();
            State.SelectedNode = null;
            State.AvailableFinishNodes.Clear();
            State.ProductParts.Clear();
            State.SelectedTopPartId = 0;
            State.AvailableTopParts.Clear();
            State.AvailableFinishNodes.Clear();
        }
}