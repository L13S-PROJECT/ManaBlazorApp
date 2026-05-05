namespace ManiApi.DTOs.Tasks
{
    public class ReadyDetailPartDto
{
    public int ProductToPartId { get; set; }
    public string Name { get; set; } = "";
    public int Qty { get; set; }
    public string State { get; set; } = "";
}

}