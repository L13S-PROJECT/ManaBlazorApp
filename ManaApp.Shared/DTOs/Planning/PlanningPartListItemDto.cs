namespace ManaApp.Shared.DTOs.Planning
{
    public class PlanningPartListItemDto
    {
        public int TopPartId { get; set; }

        public string PartName { get; set; } = "";

        public string PartCode { get; set; } = "";

        public int PlanQty { get; set; }
    }
}