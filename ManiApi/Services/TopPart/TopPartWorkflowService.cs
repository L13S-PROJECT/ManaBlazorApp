using ManiApi.Data;
using ManiApi.Models;

namespace ManiApi.Services.TopParts
{
    public class TopPartWorkflowService
    {
        private readonly AppDbContext _db;

        public TopPartWorkflowService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<int> CreateInitialWorkflowAsync(
                int topPartId,
                string topPartName)
            {
               var workflow = new ManiApi.Models.Workflow
                {
                    TopPartId = (uint)topPartId,
                    WorkflowVersion = 1,
                    Status = WorkflowStatus.Draft,
                    VersionId = null,
                    ParentNodeId = null,
                    Name = $"{topPartName} - V1",
                    CreatedDate = DateTime.Now,
                    IsCurrent = false,
                    IsActive = true
                };

                _db.Workflows.Add(workflow);
                await _db.SaveChangesAsync();

                var partNode = new WorkflowNode
                {
                    WorkflowId = workflow.Id,
                    NodeType = (byte)WorkflowNodeType.Part,
                    Name = topPartName,
                    TopPartId = (uint)topPartId,
                    SortOrder = 10,
                    IsActive = true
                };

                var finishNode = new WorkflowNode
                {
                    WorkflowId = workflow.Id,
                    NodeType = (byte)WorkflowNodeType.Finish,
                    Name = "FINISH",
                    TopPartId = (uint)topPartId,
                    SortOrder = 20,
                    IsActive = true
                };

                _db.WorkflowNodes.AddRange(partNode, finishNode);
                await _db.SaveChangesAsync();

                _db.WorkflowNodeConnections.Add(
                    new WorkflowNodeConnection
                    {
                        FromNodeId = partNode.Id,
                        ToNodeId = finishNode.Id
                    });

                await _db.SaveChangesAsync();

                return workflow.Id;
            }
    }
}