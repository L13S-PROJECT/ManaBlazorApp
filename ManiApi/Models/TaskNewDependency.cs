namespace ManiApi.Models
{
    public class TaskNewDependency
    {
        public uint ID { get; set; }

        public uint TaskNew_ID { get; set; }

        public uint DependsOnTaskNew_ID { get; set; }

        public TaskNew? TaskNew { get; set; }

        public TaskNew? DependsOnTaskNew { get; set; }
    }
}
