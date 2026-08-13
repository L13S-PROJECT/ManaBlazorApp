using System.Net.Http.Json;
using ManaApp.Shared.DTOs.TopPart;

namespace ManaApp.Services
{
    public class TopPartWorkflowService
    {
        private readonly HttpClient _http;

        public TopPartWorkflowService(HttpClient http)
            {
                _http = http;
            }

        public async Task<TopPartWorkflowDto?> GetWorkflowAsync(int workflowId)
            {
                return await _http.GetFromJsonAsync<TopPartWorkflowDto>(
                    $"api/TopPartWorkflow/{workflowId}");
            }

        public async Task<List<TopPartWorkflowVersionDto>> GetVersionsAsync(int topPartId)
            {
                return await _http.GetFromJsonAsync<List<TopPartWorkflowVersionDto>>(
                    $"api/TopPartWorkflow/toppart/{topPartId}/versions")
                    ?? new();
            }

        public string? LastError { get; private set; }

        public async Task<int?> CreateAsync(int topPartId)
            {
                LastError = null;

                var request = new CreateTopPartWorkflowRequest
                {
                    TopPartId = (uint)topPartId
                };

                var response = await _http.PostAsJsonAsync(
                    "api/TopPartWorkflow",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return null;
                }

                var workflow =
                    await response.Content.ReadFromJsonAsync<TopPartWorkflowVersionDto>();

                return workflow?.Id;
            }

        public async Task<int?> GetDisplayWorkflowIdAsync(int topPartId)
            {
                var response = await _http.GetAsync(
                    $"api/TopPartWorkflow/toppart/{topPartId}/display");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<int>();
            }

        public async Task<bool> ReleaseAsync(int workflowId)
            {
                LastError = null;

                var response = await _http.PostAsync(
                    $"api/TopPartWorkflow/{workflowId}/release",
                    null);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return false;
                }

                return true;
            }

        public async Task<int?> EditAsync(int workflowId)
            {
                LastError = null;

                var response = await _http.PostAsync(
                    $"api/TopPartWorkflow/{workflowId}/edit",
                    null);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return null;
                }

                var result = await response.Content
                    .ReadFromJsonAsync<Dictionary<string, object>>();

                if (result is not null &&
                    result.TryGetValue("workflowId", out var workflowIdValue))
                        {
                            return Convert.ToInt32(workflowIdValue.ToString());
                        }

                return null;
            }

        public async Task<bool> AddProcessAsync(CreateTopPartProcessRequest model)
            {
                LastError = null;

                var response = await _http.PostAsJsonAsync(
                    "api/TopPartWorkflow/process",
                    model);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return false;
                }

                return true;
            }

        public async Task<bool> DeleteProcessAsync(
                int workflowId,
                int processNodeId)
            {
                LastError = null;

                var response = await _http.DeleteAsync(
                    $"api/TopPartWorkflow/process/{workflowId}/{processNodeId}");

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return false;
                }

                return true;
            }

        public async Task<bool> UpdateProcessAsync(UpdateTopPartProcessRequest model)
            {
                LastError = null;

                var response = await _http.PutAsJsonAsync(
                    "api/TopPartWorkflow/process",
                    model);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return false;
                }

                return true;
            }

        public async Task<List<TopPartBomPartOptionDto>> GetBomPartOptionsAsync(
                int workflowId)
            {
                return await _http.GetFromJsonAsync<List<TopPartBomPartOptionDto>>(
                    $"api/TopPartWorkflow/{workflowId}/bom/parts")
                    ?? new();
            }

        public async Task<bool> AddBomPartAsync(
                int workflowId,
                AddTopPartBomPartRequest model)
            {
                LastError = null;

                var response = await _http.PostAsJsonAsync(
                    $"api/TopPartWorkflow/{workflowId}/bom/part",
                    model);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return false;
                }

                return true;
            }
        
        public async Task<bool> AddBomItemAsync(
                int workflowId,
                AddTopPartBomItemRequest model)
            {
                LastError = null;

                var response = await _http.PostAsJsonAsync(
                    $"api/TopPartWorkflow/{workflowId}/bom/item",
                    model);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return false;
                }

                return true;
            }

        public async Task<List<TopPartWorkflowBomDto>> GetBomAsync(int workflowId)
            {
                return await _http.GetFromJsonAsync<List<TopPartWorkflowBomDto>>(
                    $"api/TopPartWorkflow/{workflowId}/bom")
                    ?? new();
            }

        public async Task<bool> AddProcessComponentAsync(
                int workflowId,
                AddTopPartProcessComponentRequest model)
            {
                LastError = null;

                var response = await _http.PostAsJsonAsync(
                    $"api/TopPartWorkflow/{workflowId}/process/component",
                    model);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return false;
                }

                return true;
            }

    }
}