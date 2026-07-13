namespace ManiApi.DTOs.WorkFlow;

public class WorkflowValidationResultDto
{
    public bool IsValid { get; set; }

    public List<WorkflowValidationErrorDto> Errors { get; set; } = new();
}