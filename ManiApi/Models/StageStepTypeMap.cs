namespace ManiApi.Models
{
    public class StageStepTypeMap
    {
        public byte Stage { get; set; }

        public int Step_Type_ID { get; set; }

        public bool IsActive { get; set; }

        public StepType? StepType { get; set; }
    }
}