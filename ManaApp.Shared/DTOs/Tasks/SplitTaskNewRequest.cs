namespace ManaApp.Shared.DTOs.Tasks
{
    public class SplitTaskNewRequest
    {
        public List<SplitTaskNewPartRequest> Parts { get; set; } = [];
    }

    public class SplitTaskNewPartRequest
    {
        public int? EmployeeId { get; set; }

        public int Quantity { get; set; }
    }
}