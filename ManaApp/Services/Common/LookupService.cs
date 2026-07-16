using System.Net.Http.Json;
using ManaApp.Models.Lookup;

namespace ManaApp.Services.Common;

public class LookupService
{
    private readonly HttpClient _http;

    public LookupService(HttpClient http)
    {
        _http = http;
    }

   public async Task<List<LookupItem>> LoadWorkCentersAsync()
    {
        var rows = await _http.GetFromJsonAsync<List<WorkCenterLookupDto>>(
            "http://localhost:5270/api/workcenters");

        if (rows == null)
            return new();

        return rows
            .OrderBy(x => x.Order)
            .Select(x => new LookupItem
            {
                Id = x.Id,
                Name = x.Name,
                Order = x.Order
            })
            .ToList();
    }
}