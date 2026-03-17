namespace ManiApi.Models
{
    public class EmployeeAvailability
    {
        public int ID { get; set; }

        public int EmployeeID { get; set; }

        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public string Status { get; set; } = "";

        public decimal? Hours { get; set; }

        public string? Notes { get; set; }
    }
}