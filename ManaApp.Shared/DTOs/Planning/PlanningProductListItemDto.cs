namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningProductListItemDto
    {
        public int TopPartId { get; set; }

        public string ProductName { get; set; } = "";

        public string ProductCode { get; set; } = "";

        public int? CategoryId { get; set; }

        public string CategoryName { get; set; } = "";

        public string ParentCategoryName { get; set; } = "";

        public int? TopPartCategoryId { get; set; }

        public int OrderQty { get; set; }

        public int InStockQty { get; set; }

        public int PlanQty { get; set; }

        public int WaitingQty { get; set; }

        public int InProductionQty { get; set; }

        public int WipQty { get; set; }

        public int PaintingQty { get; set; }
    }
}
