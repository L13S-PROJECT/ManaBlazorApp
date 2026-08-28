using System.Net.Http.Json;
using ManaApp.Shared.DTOs.Planning;

namespace ManaApp.Services.TopPartPlanning
{
    public class TopPartPlanningService
    {
        private readonly HttpClient _http;

        public TopPartPlanningService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<PlanningProductListItemDto>> GetProductsAsync()
        {
            return await _http.GetFromJsonAsync<List<PlanningProductListItemDto>>(
                "api/production-planning/products") ?? [];
        }

        public async Task<List<PlanningWorkflowOptionDto>> GetWorkflowsAsync(
                int topPartId)
            {
                return await _http.GetFromJsonAsync<List<PlanningWorkflowOptionDto>>(
                    $"api/production-planning/products/{topPartId}/workflows") ?? [];
            }

        public async Task SaveDraftItemAsync(
                SavePlanningDraftItemRequest request)
            {
                var response = await _http.PostAsJsonAsync(
                    "api/production-planning/draft/items",
                    request);

                response.EnsureSuccessStatusCode();
            }

        public async Task<List<PlanningCorrectionBatchOptionDto>>
                GetCorrectionBatchesAsync()
            {
                return await _http.GetFromJsonAsync<
                    List<PlanningCorrectionBatchOptionDto>>(
                    "api/production-planning/correction/batches") ?? [];
            }

        public async Task<PlanningCorrectionBatchDto?> GetCorrectionBatchAsync(
                uint batchId)
            {
                return await _http.GetFromJsonAsync<PlanningCorrectionBatchDto>(
                    $"api/production-planning/correction/batches/{batchId}");
            }

        public async Task SaveDraftAsync(
                SavePlanningDraftRequest request)
            {
                var response = await _http.PostAsJsonAsync(
                    "api/production-planning/draft/save",
                    request);

                response.EnsureSuccessStatusCode();
            }

        public async Task DeleteDraftAsync()
            {
                var response = await _http.DeleteAsync(
                    "api/production-planning/draft");

                response.EnsureSuccessStatusCode();
            }

        public async Task<List<PlanningDraftItemDto>> GetDraftItemsAsync()
            {
                return await _http.GetFromJsonAsync<List<PlanningDraftItemDto>>(
                    "api/production-planning/draft/items") ?? [];
            }

        public async Task DeleteDraftItemAsync(uint draftItemId)
            {
                var response = await _http.DeleteAsync(
                    $"api/production-planning/draft/items/{draftItemId}");

                response.EnsureSuccessStatusCode();
            }

        public async Task UpdateDraftItemAsync(
                uint draftItemId,
                UpdatePlanningDraftItemRequest request)
            {
                var response = await _http.PutAsJsonAsync(
                    $"api/production-planning/draft/items/{draftItemId}",
                    request);

                response.EnsureSuccessStatusCode();
            }

        public async Task UpdateCorrectionBatchAsync(
                uint batchId,
                UpdatePlanningCorrectionBatchRequest request)
            {
                var response = await _http.PutAsJsonAsync(
                    $"api/production-planning/correction/batches/{batchId}",
                    request);

                response.EnsureSuccessStatusCode();
            }

        public async Task DeleteCorrectionBatchAsync(uint batchId)
            {
                var response = await _http.DeleteAsync(
                    $"api/production-planning/correction/batches/{batchId}");

                response.EnsureSuccessStatusCode();
            }

    }
}