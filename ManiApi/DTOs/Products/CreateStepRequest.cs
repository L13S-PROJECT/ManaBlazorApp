namespace ManiApi.DTOs.Products
{
        public class CreateStepRequest
{
    public int ProductToPartId { get; set; }   // ProductTopPart.Id
    public int StepOrder { get; set; }         // ja 0 → likšu max+10
    public string StepName { get; set; } = "";
    public int StepType { get; set; }          // StepTypes.Id
    public int WorkCentrId { get; set; }       // WorkCentrs.Id
    public int? EstimatedMinutes { get; set; }
    public int ParallelGroup { get; set; } = 0;
    public bool IsMandatory { get; set; }
    public bool IsFinal { get; set; }
    public bool IsPainting { get; set; }
    public string? Comments { get; set; }
}
}