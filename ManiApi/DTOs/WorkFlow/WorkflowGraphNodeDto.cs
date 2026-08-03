using ManiApi.Models;

namespace ManiApi.DTOs.WorkFlow;

public class WorkflowGraphNodeDto
{
    public WorkflowNode Node { get; set; } = null!;

    public List<WorkflowGraphNodeDto> Next { get; set; } = new();

    public List<WorkflowGraphNodeDto> Previous { get; set; } = new();
}