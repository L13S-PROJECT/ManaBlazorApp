namespace ManaApp.Models
{
    public class WorksByPartDto
    {
        public string? TopPartName { get; set; }
        public string? TopPartCode { get; set; }
        public List<WorkStepItem>? Steps { get; set; }
    }

    public class WorkStepItem
    {
        public int StepOrder { get; set; }
        public string? StepName { get; set; }
        public string? StepType { get; set; }
        public string? WorkCenter { get; set; }
        public bool IsFinal { get; set; }
        public bool IsMandatory { get; set; }
        public string? Comments { get; set; }
    }
}
