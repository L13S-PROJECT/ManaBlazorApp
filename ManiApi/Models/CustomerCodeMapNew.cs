using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManiApi.Models
{
    [Table("customer_code_map_new")]
    public class CustomerCodeMapNew
    {
        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("Customer_Name")]
        public string CustomerName { get; set; } = "";

        [Column("Customer_Code")]
        public string CustomerCode { get; set; } = "";

        [Column("TopPart_ID")]
        public int TopPartId { get; set; }

        [Column("Workflow_ID")]
        public int WorkflowId { get; set; }

        [Column("Ral_Color_ID")]
        public int? RalColorId { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
    }
}