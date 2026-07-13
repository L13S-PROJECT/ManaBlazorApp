namespace ManiApi.DTOs.WorkFlow;

public class WorkflowValidationErrorDto
{
    public int? NodeId { get; set; }

    public string Message { get; set; } = "";
}