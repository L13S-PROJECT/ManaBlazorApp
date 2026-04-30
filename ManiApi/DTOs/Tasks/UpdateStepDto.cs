namespace ManiApi.DTOs.Tasks
{
    public sealed class UpdateStepDto
{
    // Kurš konkrētais tasks (tasks.ID)
    public int TaskId { get; set; }

    // Vai solis ir prioritārs (var nebūt padots -> atstājam kā ir)
    public bool? Tasks_Priority { get; set; }

    public bool? Tasks_Push { get; set; }

    // Kam tiek piešķirts (var būt null -> noņemam Assignment)
    public int? Assigned_To { get; set; }
}
}