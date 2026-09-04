namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningPartRequirementDetailDto
    {
        public string BatchCode { get; set; } = "";

        public string SourceTopPartName { get; set; } = "";

        public string SourceTopPartCode { get; set; } = "";

        public int WorkflowVersion { get; set; }

        public string ProcessName { get; set; } = "";

        public string NodeType { get; set; } = "";

        public string NodeName { get; set; } = "";

        public int GrossQuantity { get; set; }

        public int StockCoveredQuantity { get; set; }

        public int IncomingCoveredQuantity { get; set; }

        public int NetQuantity { get; set; }
    }
}
