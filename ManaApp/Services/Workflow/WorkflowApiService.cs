using System.Net.Http.Json;
using ManaApp.Models;
using ManaApp.DTOs.Workflow;
using ManaApp.ViewModels.Workflow;

namespace ManaApp.Services.Workflow;

public class WorkflowService
{
    private readonly HttpClient _http;

    public WorkflowService(HttpClient http)
    {
        _http = http;
    }

    public async Task<WorkflowDto?> LoadWorkflowAsync(int versionId)
        {
            return await _http.GetFromJsonAsync<WorkflowDto>(
                $"http://localhost:5270/api/workflow/{versionId}");
        }

    public Dictionary<int, WorkflowGraphNode> BuildGraph(WorkflowDto workflow)
        {
            var graph = new Dictionary<int, WorkflowGraphNode>();

            foreach (var node in workflow.Nodes)
            {
                graph[node.Id] = new WorkflowGraphNode
                {
                    Node = node
                };
            }

            foreach (var connection in workflow.Connections)
            {
                if (!graph.TryGetValue(connection.FromNodeId, out var from))
                    continue;

                if (!graph.TryGetValue(connection.ToNodeId, out var to))
                    continue;

                from.Next.Add(to);
                to.Previous.Add(from);
            }

            return graph;
        }

        public bool TrySelectNode(
            Dictionary<int, WorkflowGraphNode> graph,
            int nodeId,
            out WorkflowGraphNode? node)
        {
            return graph.TryGetValue(nodeId, out node);
        }

        public async Task<List<MergeFinishItemDto>?> LoadFinishNodesAsync(int workflowId)
        {
            return await _http.GetFromJsonAsync<List<MergeFinishItemDto>>(
                $"http://localhost:5270/api/workflow/finish/{workflowId}");
        }

        public List<MergeFinishItem> CreateFinishItems(List<MergeFinishItemDto>? items)
            {
                var result = new List<MergeFinishItem>();

                if (items == null)
                    return result;

                foreach (var item in items)
                {
                    result.Add(new MergeFinishItem
                    {
                        Id = item.Id,
                        Name = item.Name ?? "",
                        Selected = false
                    });
                }

                return result;
            }
        
        public async Task<WorkflowState?> LoadStateAsync(int versionId)
            {
                var workflow = await LoadWorkflowAsync(versionId);

                if (workflow == null)
                    return null;

                return new WorkflowState
                {
                    Workflow = workflow,
                    Graph = BuildGraph(workflow)
                };
            }

}