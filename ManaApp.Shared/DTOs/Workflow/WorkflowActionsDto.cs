namespace ManaApp.Shared.DTOs.Workflow;

public class WorkflowActionsDto
{
    public bool CanAddTopPart { get; set; }
    public bool CanAddProcess { get; set; }

    public bool CanAddSubPart { get; set; }

    public bool CanAddFinish { get; set; }
}