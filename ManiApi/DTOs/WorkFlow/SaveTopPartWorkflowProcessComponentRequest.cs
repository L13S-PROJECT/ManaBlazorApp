namespace ManiApi.DTOs.WorkFlow;

public class SaveTopPartWorkflowProcessComponentRequest
{
    public int WorkflowComponentId { get; set; }

    public decimal Quantity { get; set; }

    public bool RequiresStaging { get; set; } = true;
}
