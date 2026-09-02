namespace ManaApp.Shared.DTOs.Planning;

public sealed class PlanningProductionFlowItemDto
{
    public string BatchCode { get; set; } = "";

    public int WorkflowVersion { get; set; }

    public int PlannedQty { get; set; }

    public int InProductionQty { get; set; }
}