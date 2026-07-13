public class AddProcessRequest
{
    public int WorkflowId { get; set; }

    public int PreviousNodeId { get; set; }

    public string ProcessName { get; set; } = "";
}