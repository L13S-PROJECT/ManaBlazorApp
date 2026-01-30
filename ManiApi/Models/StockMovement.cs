namespace ManiApi.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("stock_movements")]
    public class StockMovement
    {
        [Column("ID")]
        public int Id { get; set; }

        [Column("Version_ID")]
        public int Version_ID { get; set; }

        // JAUNS LAUKS
        [Column("BatchProduct_ID")]
        public int? BatchProduct_ID { get; set; }   // var būt NULL

        [Column("Move_Type")]
        public MoveType Move_Type { get; set; }

        [Column("Stock_Qty")]
        public int Stock_Qty { get; set; }

        [Column("Created_At")]
        public DateTime Created_At { get; set; }

        [Column("Task_ID")]
        public int? Task_ID { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

     }
}
