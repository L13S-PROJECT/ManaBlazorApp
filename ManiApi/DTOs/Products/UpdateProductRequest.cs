namespace ManiApi.DTOs.Products
{
   public class UpdateProductRequest
{
    public int ProductId { get; set; }
    public bool IsHistoricalVersion { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public int CategoryId { get; set; }

    public bool CreateNewVersion { get; set; }  // true → izveido jaunu versiju
    public int? VersionId { get; set; }         // vajadzīgs, ja labo esošo (CreateNewVersion=false)
    public int ProductionModel { get; set; } = 0;
    public string? VersionName { get; set; }
    public string? VersionRasejums { get; set; }
    public string? VersionDate { get; set; }    // "yyyy-MM-dd"
    public string? VersionComment { get; set; }
    public bool CopyTechnologySteps { get; set; } // true -> kopēt soļus jaunajai versijai

}
}