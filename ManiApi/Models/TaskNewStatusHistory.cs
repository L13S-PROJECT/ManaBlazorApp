namespace ManiApi.Models
{
    public class TaskNewStatusHistory
    {
        public uint ID { get; set; }

        public uint TaskNew_ID { get; set; }

        public TaskNewStatus? FromStatus { get; set; }

        public TaskNewStatus ToStatus { get; set; }

        public int? ChangedByEmployee_ID { get; set; }

        public DateTime Changed_At { get; set; } = DateTime.UtcNow;

        public string? Comment { get; set; }

        public TaskNew? TaskNew { get; set; }

        public Employee? ChangedByEmployee { get; set; }
    }
}
