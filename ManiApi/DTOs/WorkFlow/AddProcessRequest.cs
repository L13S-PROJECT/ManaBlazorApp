public class AddProcessRequest
{
    public int WorkflowId { get; set; }

    public int SelectedNodeId { get; set; }

    public string ProcessName { get; set; } = "";
}