namespace ManiApi.DTOs.Tasks;

public sealed class StartPaintingSessionRequest
{
    public int EmployeeId { get; set; }

    public List<int> TaskIds { get; set; } = new();
}