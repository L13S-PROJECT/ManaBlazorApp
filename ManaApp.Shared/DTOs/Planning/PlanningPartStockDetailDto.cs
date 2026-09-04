namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningPartStockDetailDto
    {
        public int WorkflowId { get; set; }

        public int WorkflowVersion { get; set; }

        public string NodeType { get; set; } = "";

        public string NodeName { get; set; } = "";

        public int StockQty { get; set; }

        public int ReservedQty { get; set; }

        public int FreeQty { get; set; }
    }
}
