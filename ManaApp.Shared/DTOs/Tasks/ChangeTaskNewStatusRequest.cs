namespace ManaApp.Shared.DTOs.Tasks
{
    public class ChangeTaskNewStatusRequest
    {
        public int Status { get; set; }

        public int? ChangedByEmployeeId { get; set; }

        public string? Comment { get; set; }
    }
}