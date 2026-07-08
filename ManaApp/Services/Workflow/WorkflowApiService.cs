using System.Net.Http.Json;
using ManaApp.Models;
using ManaApp.DTOs.Workflow;
using ManaApp.ViewModels.Workflow;

namespace ManaApp.Services.Workflow;

public class WorkflowApiService
{
    private readonly HttpClient _http;

    public WorkflowApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<WorkflowDto?> LoadWorkflowAsync(int versionId)
        {
            try
            {
                return await _http.GetFromJsonAsync<WorkflowDto>(
                    $"http://localhost:5270/api/workflow/{versionId}");
            }
            catch (HttpRequestException)
            {
                return null;
            }
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
                
                var productParts = await LoadProductPartsAsync(versionId);

                var graph = BuildGraph(workflow);

                var partIndex = graph.Values
                    .Where(x => x.Node?.ProductToPartId != null)
                    .GroupBy(x => x.Node!.ProductToPartId!.Value)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(x => x.Node.Id).First());

                return new WorkflowState
                    {
                        Workflow = workflow,
                        Graph = graph,
                        PartNodeByProductToPartId = partIndex,
                        ProductParts = productParts
                    };
            }


        public async Task<List<WorkflowPartModel>> LoadProductPartsAsync(int versionId)
            {
                return await _http.GetFromJsonAsync<List<WorkflowPartModel>>(
                    $"http://localhost:5270/api/workflow/productparts/{versionId}")
                    ?? new();
            }
        
        public async Task<HttpResponseMessage> CreateNodeAsync(CreateWorkflowNodeRequest request)
            {
                return await _http.PostAsJsonAsync(
                    "http://localhost:5270/api/workflow/node",
                    request);
            }
        
        public async Task<WorkflowNodeModel?> CreatePartNodeAsync(
            int workflowId,
            WorkflowPartModel part)
        {
            var response = await CreateNodeAsync(new CreateWorkflowNodeRequest
            {
                WorkflowId = workflowId,
                NodeType = 1,
                Name = part.TopPartName,
                ProductToPartId = part.ProductToPartId
            });

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<WorkflowNodeModel>();
        }
        
        public async Task<HttpResponseMessage> CreateConnectionAsync(CreateWorkflowConnectionRequest request)
            {
                return await _http.PostAsJsonAsync(
                    "http://localhost:5270/api/workflow/connect",
                    request);
            }

        public async Task<List<WorkflowTopPartSelectDto>> LoadAvailableTopPartsAsync(int versionId)
            {
                return await _http.GetFromJsonAsync<List<WorkflowTopPartSelectDto>>(
                    $"http://localhost:5270/api/workflow/available-topparts?versionId={versionId}")
                    ?? new();
            }
        
        public async Task<List<WorkflowTopPartSelectDto>> LoadAvailableSubPartsAsync(int versionId)
            {
                return await _http.GetFromJsonAsync<List<WorkflowTopPartSelectDto>>(
                    $"http://localhost:5270/api/workflow/available-subparts?versionId={versionId}")
                    ?? new();
            }

        public async Task<HttpResponseMessage> AddTopPartAsync(
            int versionId,
            int topPartId,
            int? parentProductTopPartId)
        {
            return await _http.PostAsJsonAsync(
                "http://localhost:5270/api/workflow/toppart",
                new
                {
                    VersionId = versionId,
                    TopPartId = topPartId,
                    ParentProductTopPartId = parentProductTopPartId
                });
        }

}