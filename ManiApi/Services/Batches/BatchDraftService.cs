using System.Net.Http.Json;
using ManaApp.Shared.DTOs.Batches;


namespace ManaApp.Services.Batches;

public sealed class BatchDraftService
{
    private readonly HttpClient _http;

    public BatchDraftService(HttpClient http)
    {
        _http = http;
    }

    public async Task<HttpResponseMessage> CreateDraft(CreateDraftRequestDto dto)
    {
        return await _http.PostAsJsonAsync(
            "http://localhost:5270/api/batches/draft/create",
            dto
        );
    }

    public async Task<HttpResponseMessage> UpdateDraft(DraftUpdateRequestDto dto)
    {
        return await _http.PostAsJsonAsync(
            "http://localhost:5270/api/batches/draft/update",
            dto
        );
    }

    public async Task<HttpResponseMessage> DeleteDraft(int batchId)
    {
        return await _http.PostAsJsonAsync(
            "http://localhost:5270/api/batches/draft/delete",
            new { batchId }
        );
    }

    public async Task<Dictionary<string, int>?> CreatePlanned(int batchId, string code)
        {
            var resp = await _http.PostAsJsonAsync(
                "http://localhost:5270/api/batches/planned",
                new
                {
                    batchId,
                    code
                });

            if (!resp.IsSuccessStatusCode)
                return null;

            return await resp.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        }
}