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


// TODO:
// Graph ir pagaidu UI modelis.
// Pakāpeniski aizstāt ar WorkflowFlowAnalyzer rezultātiem no API.
// Pēc Analyzer migrācijas šo metodi varēs dzēst.
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

        

        public async Task<List<MergeFinishItemDto>?> LoadFinishNodesAsync(int workflowId)
        {
            return await _http.GetFromJsonAsync<List<MergeFinishItemDto>>(
                $"http://localhost:5270/api/workflow/finish/{workflowId}");
        }
        
        public async Task<WorkflowState?> LoadStateAsync(int versionId)
            {
                var workflow = await LoadWorkflowAsync(versionId);

                if (workflow == null)
                    return null;
                
                var productParts = await LoadProductPartsAsync(versionId);
                var availableFlows = await LoadAvailableFlowsAsync(workflow.Workflow!.Id);

// TODO:
// Pagaidu risinājums.
// Graph tiek būvēts UI vajadzībām.
// Pēc WorkflowFlowAnalyzer migrācijas Graph tiks saņemts no API vai vairs nebūs nepieciešams.
                
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
                        ProductParts = productParts,
                        AvailableFlows = availableFlows
                            .Select(x => new MergeFlowItem
                            {
                                Flow = x
                            })
                            .ToList(),
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
            int? parentProductTopPartId,
            int? attachToNodeId)
        {
            return await _http.PostAsJsonAsync(
                "http://localhost:5270/api/workflow/toppart",
                new
                {
                    VersionId = versionId,
                    TopPartId = topPartId,
                    ParentProductTopPartId = parentProductTopPartId,
                    AttachToNodeId = attachToNodeId
                });
        }

        public async Task<HttpResponseMessage> AddSubPartAsync(
            int versionId,
            int topPartId,
            int parentProductTopPartId,
            int attachToNodeId)
        {
            return await _http.PostAsJsonAsync(
                "http://localhost:5270/api/workflow/subpart",
                new
                {
                    VersionId = versionId,
                    TopPartId = topPartId,
                    ParentProductTopPartId = parentProductTopPartId,
                    AttachToNodeId = attachToNodeId
                });
        }

        public async Task<HttpResponseMessage> AddProcessAsync(
                int workflowId,
                int previousNodeId,
                string processName)
            {
                return await _http.PostAsJsonAsync(
                    "http://localhost:5270/api/workflow/process",
                    new
                    {
                        WorkflowId = workflowId,
                        PreviousNodeId = previousNodeId,
                        ProcessName = processName
                    });
            }

       public async Task<HttpResponseMessage> AddMergeAsync(
            int workflowId,
            int currentFinishNodeId,
            List<int> mergeFinishNodeIds)
            {
                return await _http.PostAsJsonAsync(
                    "http://localhost:5270/api/workflow/merge",
                    new
                        {
                            WorkflowId = workflowId,
                            CurrentFinishNodeId = currentFinishNodeId,
                            MergeFinishNodeIds = mergeFinishNodeIds
                        });
            }

        public async Task<HttpResponseMessage> CreateWorkflowAsync(int versionId)
            {
                return await _http.PostAsJsonAsync(
                    "http://localhost:5270/api/workflow",
                    new
                    {
                        VersionId = versionId,
                        WorkflowName = "Workflow"
                    });
            }

        public async Task<HttpResponseMessage> SaveNodeCommentsAsync(
            int nodeId,
            string? comments)
        {
            return await _http.PostAsJsonAsync(
                "http://localhost:5270/api/workflow/node/comments",
                new
                {
                    NodeId = nodeId,
                    Comments = comments
                });
        }

        public async Task<HttpResponseMessage> SaveQtyPerProductAsync(
            int productToPartId,
            int qtyPerProduct)
        {
            return await _http.PostAsJsonAsync(
                "http://localhost:5270/api/workflow/part/qty",
                new
                {
                    ProductToPartId = productToPartId,
                    QtyPerProduct = qtyPerProduct
                });
        }

        public async Task<HttpResponseMessage> SaveProcessAsync(
            WorkflowNodeModel node)
        {
            return await _http.PostAsJsonAsync(
                "http://localhost:5270/api/workflow/process/save",
                new
                {
                    NodeId = node.Id,
                    Name = node.Name,
                    WorkCenterId = node.WorkCenterId,
                    EstimatedMinutes = node.EstimatedMinutes,
                    Comments = node.Comments
                });
        }

        public async Task<List<AvailableFlowDto>> LoadAvailableFlowsAsync(int workflowId)
            {
                return await _http.GetFromJsonAsync<List<AvailableFlowDto>>(
                    $"http://localhost:5270/api/workflow/available-flows/{workflowId}")
                    ?? new();
            }

        public async Task<HttpResponseMessage> AddFinishAsync(
            int workflowId,
            int flowOwnerNodeId)
        {
            return await _http.PostAsJsonAsync(
                "http://localhost:5270/api/workflow/finish",
                new
                {
                    WorkflowId = workflowId,
                    FlowOwnerNodeId = flowOwnerNodeId
                });
        }


}