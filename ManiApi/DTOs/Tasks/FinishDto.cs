namespace ManiApi.DTOs.Tasks
{
          public sealed class FinishDto
{
    public int TaskId { get; set; }

    // Tikai Finishing gadījumam:
    // cik gabalus darbinieks pabeidza šajā reizē
    public int? QtyDoneAdd { get; set; }
}  
}