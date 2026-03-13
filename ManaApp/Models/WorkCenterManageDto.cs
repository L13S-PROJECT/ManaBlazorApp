namespace ManaApp.Models
{
    public class WorkCenterManageDto
{
    public int Id { get; set; }
    public string WorkCentr_Name { get; set; } = "";
    public int WorkCenter_Order { get; set; }
    public int? Step_Type_ID { get; set; }
    public bool IsActive { get; set; }
}
}