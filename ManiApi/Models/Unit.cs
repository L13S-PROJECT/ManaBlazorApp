namespace ManiApi.Models;

public class Unit
{
    public int Id { get; set; }

    public string UnitCode { get; set; } = "";

    public string UnitName { get; set; } = "";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}
