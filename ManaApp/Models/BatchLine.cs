using System.Text.Json.Serialization;
using ManaApp.Shared.DTOs.Planning;
namespace ManaApp.Models;

public sealed class BatchLine
{
    [JsonPropertyName("BatchId")]        public int BatchId { get; set; }
    [JsonPropertyName("BatchCode")]      public string BatchCode { get; set; } = "";
    [JsonPropertyName("BatchProductId")] public int BatchProductId { get; set; }
    [JsonPropertyName("Version_Id")]     public int VersionId { get; set; }
    [JsonPropertyName("versionName")] public string? VersionName { get; set; }
    [JsonPropertyName("SelectedParts")] public List<ProductToPartDto>? SelectedParts { get; set; }
    [JsonPropertyName("Planned")]        public int Planned { get; set; }
    [JsonPropertyName("Detailed")]       public int Detailed { get; set; }
    [JsonPropertyName("Assembly")]       public int Assembly { get; set; }
    [JsonPropertyName("Finishing")]      public int Finishing { get; set; } // <- šis tev trūkst
    [JsonPropertyName("Done")]           public int Done { get; set; }      // <- Done = STOCK
    [JsonPropertyName("Stock")]          public int Stock { get; set; }
    [JsonPropertyName("Comment")]        public string? Comment { get; set; }
    [JsonPropertyName("ProductToPartId")] public int? ProductToPartId { get; set; }
    [JsonPropertyName("StartedAt")]      public DateTime? StartedAt { get; set; }
    [JsonPropertyName("BatchStatus")]    public int BatchStatus { get; set; }
}