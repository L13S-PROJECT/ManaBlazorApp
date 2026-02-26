namespace ManaApp.Models
{
    public class ProductDetailDto
    {
        public string? TopPartName { get; set; }
        public string? TopPartCode { get; set; }
        public int Quantity { get; set; }
        public int ProductToPartId { get; set; }
        public int Stage { get; set; }
    }

    public class PartStepDto
    {
        public int Id { get; set; }
        public int ProductToPartId { get; set; }
        public int StepOrder { get; set; }
        public string StepName { get; set; } = "";
        public int StepType { get; set; }
        public int WorkCentrId { get; set; }
        public int ParallelGroup { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsFinal { get; set; }
        public string? Comments { get; set; }

        public bool IsParallel
        {
            get => ParallelGroup > 0;
            set => ParallelGroup = value ? 1 : 0;
        }
    }

    public class StepTypeDto
        {
            public int Id { get; set; }
            public string StepTypeName { get; set; } = "";
            public bool IsActive { get; set; }
        }
    
    public class WorkCenter
        {
            public int Id { get; set; }
            public string WorkCentr_Name { get; set; } = "";
            public bool IsActive { get; set; }
        }
}