using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ManiApi.Models
{
    [Table("tasks")]
    public class Tasks
    {
        [Key]
        [Column("ID")]
        public int ID { get; set; }
        
        [Column("BatchProduct_ID")]
        public int BatchProduct_ID { get; set; }
        [Column("TopPartStep_ID")]
        public int TopPartStep_ID { get; set; }
        [Column("Tasks_Priority")]
        public int Tasks_Priority { get; set; }
        [Column("Tasks_Push")]
        public bool Tasks_Push { get; set; }
        [Column("Qty_Done")]
        public int Qty_Done { get; set; }
        [Column("Qty_Scrap")]           
        public int Qty_Scrap { get; set; }
        [Column("Tasks_Status")]
        public int Tasks_Status { get; set; }

        [Column("RAL_Color_ID")]
        public int? RAL_Color_ID { get; set; }
        
        [Column("Tasks_Comment")]
        public string? Tasks_Comment { get; set; }
        [Column("Is_Comment_For_Employee")]
        public bool Is_Comment_For_Employee { get; set; }

        [Column("Started_At")]
        public DateTime? Started_At { get; set; }
        [Column("Finished_At")]     
        public DateTime? Finished_At { get; set; }
        [Column("Assigned_To")]
        public int? Assigned_To { get; set; }
        [Column("Claimed_By")]
        public int? Claimed_By { get; set; }
        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}
