namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningPartListItemDto
    {
        public int TopPartId { get; set; }

        public string PartName { get; set; } = "";

        public string PartCode { get; set; } = "";

        public int? TopPartCategoryId { get; set; }

        public int PlanQty { get; set; }
        
        public int WaitingQty { get; set; }

        public int InProductionQty { get; set; }

        public int StockQty { get; set; }

        public int ReservedQty { get; set; }

        public int FreeQty { get; set; }

    }
}