namespace ManiApi.DTOs.Tasks
{
   public sealed class DetailQtyRow
{
    public int ProductToPartId { get; set; }
    public int ParentQty { get; set; }
    public int ChildQty { get; set; }
} 
}