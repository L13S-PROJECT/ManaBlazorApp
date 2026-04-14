namespace ManaApp.Models;

public class WorkCenterDto
{
    public int ID { get; set; }
    public string WorkCentr_Name { get; set; } = "";
    public int? Step_Type_ID { get; set; }
    public int WorkCenter_Order { get; set; }
    public int IsActive { get; set; }
}