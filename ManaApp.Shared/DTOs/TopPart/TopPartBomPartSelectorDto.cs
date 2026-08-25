namespace ManaApp.Shared.DTOs.TopPart
{
    public class TopPartBomPartSelectorDto
    {
        public int TopPartId { get; set; }

        public string TopPartCode { get; set; } = "";
        public string TopPartName { get; set; } = "";

        public int ReleasedWorkflowId { get; set; }
        public int ReleasedWorkflowVersion { get; set; }
        public int? RequiredWorkflowNodeId { get; set; }
        public bool IsSelected { get; set; }
        public decimal Quantity { get; set; }
        public bool CanEdit { get; set; }
        
    }
}