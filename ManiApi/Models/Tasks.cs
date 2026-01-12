using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("tasks")]
    public class Tasks
    {
        public int ID { get; set; }
        public int BatchProduct_ID { get; set; }
        public int TopPartStep_ID { get; set; }
        public int Tasks_Priority { get; set; }
        public int Qty_Done { get; set; }
        public int Qty_Scrap { get; set; }
        public int Tasks_Status { get; set; }

        [Column("Tasks_Comment")]
        public string? Tasks_Comment { get; set; }

        public DateTime? Started_At { get; set; }
        public DateTime? Finished_At { get; set; }
        public int? Assigned_To { get; set; }
        public int? Claimed_By { get; set; }
        public bool IsActive { get; set; }
    }
}
