namespace ManaApp.Shared.DTOs.Planning;

public sealed class PlanningSparePartGroupDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public List<PlanningSparePartProductDto> Products { get; set; } = [];
}

public sealed class PlanningSparePartProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public List<PlanningSparePartDto> SpareParts { get; set; } = [];
    public List<PlanningSparePartWorkflowDto> Workflows { get; set; } = [];
}

public sealed class PlanningSparePartDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
}

public sealed class PlanningSparePartWorkflowDto
{
    public int WorkflowId { get; set; }
    public int WorkflowVersion { get; set; }
    public bool IsCurrent { get; set; }
    public List<PlanningSparePartDto> SpareParts { get; set; } = [];
}