using System.Text.Json.Serialization;

namespace ManaApp.Models.Lookup;

public class WorkCenterLookupDto
{
    public int Id { get; set; }

    [JsonPropertyName("workCentr_Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("workCenter_Order")]
    public int Order { get; set; }
}