using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("tasks_work_sessions")]
    public class TaskWorkSession
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("Task_ID")]
        public int TaskId { get; set; }

        [Column("Employee_ID")]
        public int EmployeeId { get; set; }

        [Column("StartTime")]
        public DateTime StartTime { get; set; }

        [Column("EndTime")]
        public DateTime? EndTime { get; set; }

        [Column("DurationMinutes")]
        public int? DurationMinutes { get; set; }
    }
}