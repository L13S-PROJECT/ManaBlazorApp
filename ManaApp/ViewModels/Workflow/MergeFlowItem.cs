using ManaApp.Shared.DTOs.Workflow;

namespace ManaApp.ViewModels.Workflow;

public class MergeFlowItem
{
    public AvailableFlowDto Flow { get; set; } = new();

    public bool Selected { get; set; }
}