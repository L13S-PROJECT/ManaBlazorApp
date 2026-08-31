namespace ManaApp.Shared.DTOs.Tasks
{
    public class TaskNewStatusHistoryDto
    {
        public uint Id { get; set; }

        public int? FromStatus { get; set; }

        public int ToStatus { get; set; }

        public int? ChangedByEmployeeId { get; set; }

        public string ChangedByEmployeeName { get; set; } = "";

        public DateTime ChangedAt { get; set; }

        public string? Comment { get; set; }
    }
}