namespace ManiApi.DTOs.Products
{
    public class UpdateStepRequest
{
    public int Id { get; set; }                // TopPartSteps.Id
    public int StepOrder { get; set; }
    public string StepName { get; set; } = "";
    public int StepType { get; set; }
    public int WorkCentrId { get; set; }
    public int? EstimatedMinutes { get; set; }
    public int ParallelGroup { get; set; } = 0;
    public bool IsMandatory { get; set; }
    public bool IsFinal { get; set; }
    public bool IsPainting { get; set; }
    public string? Comments { get; set; }
}
}