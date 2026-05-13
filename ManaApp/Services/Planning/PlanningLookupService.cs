using System.Net.Http.Json;
using ManaApp.Shared.DTOs.Planning;
using ManaApp.Shared.DTOs.Batches;
namespace ManaApp.Services.Planning;

public sealed class PlanningLookupService
{
    private readonly HttpClient _http;

    public PlanningLookupService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ProductToPartDto>> LoadProductParts(int versionId)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<ProductToPartDto>>(
                $"http://localhost:5270/api/topparts/by-version?versionId={versionId}"
            ) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task<bool> SaveDraftAsync(DraftUpdateRequestDto dto)
{
    try
    {
        var resp = await _http.PostAsJsonAsync(
            "http://localhost:5270/api/batches/draft/update",
            dto
        );

        return resp.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

public async Task<bool> DeleteDraftAsync(int batchId)
{
    try
    {
        var resp = await _http.PostAsJsonAsync(
            "http://localhost:5270/api/batches/draft/delete",
            new { batchId }
        );

        return resp.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

public async Task<bool> CreatePlannedBatchAsync(
    int batchId,
    string code)
{
    try
    {
        var resp = await _http.PostAsJsonAsync(
            "http://localhost:5270/api/batches/planned",
            new
            {
                batchId,
                code
            }
        );

        return resp.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

public async Task<bool> CheckBatchCodeAsync(string code)
{
    try
    {
        var resp = await _http.GetAsync(
            $"http://localhost:5270/api/batches/check-code?code={Uri.EscapeDataString(code)}"
        );

        return resp.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

public async Task<int?> CreateDraftAsync(CreateDraftRequestDto dto)
{
    try
    {
        var resp = await _http.PostAsJsonAsync(
            "http://localhost:5270/api/batches/draft/create",
            dto
        );

        if (!resp.IsSuccessStatusCode)
            return null;

        var data = await resp.Content
            .ReadFromJsonAsync<Dictionary<string, int>>();

        if (data != null &&
            data.TryGetValue("batchId", out var id))
        {
            return id;
        }

        return null;
    }
    catch
    {
        return null;
    }
}


}