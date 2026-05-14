using System.Net.Http.Json;
using ManaApp.Models;
using ManaApp.Shared.DTOs.Planning;
using ManaApp.Models.DTOs;

namespace ManaApp.Services.Planning;

public class PlanningDataService
{
    private readonly HttpClient _http;

    public PlanningDataService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ProductRow>> LoadPlanningRowsAsync()
    {
        return await _http.GetFromJsonAsync<List<ProductRow>>(
            "http://localhost:5270/api/products/planning-list")
            ?? new List<ProductRow>();
    }

    public async Task<List<BatchPlannedRow>> LoadPlannedListAsync()
    {
        return await _http.GetFromJsonAsync<List<BatchPlannedRow>>(
            "http://localhost:5270/api/batches/list?batch_type=1")
            ?? new List<BatchPlannedRow>();
    }

    public async Task<List<OrderRalDto>> LoadOrderRalsAsync()
    {
        return await _http.GetFromJsonAsync<List<OrderRalDto>>(
            "http://localhost:5270/api/orders/planning-order-rals")
            ?? new List<OrderRalDto>();
    }

    public Dictionary<int, int> BuildPlannedByVersion(
    List<BatchPlannedRow> plannedList)
        {
            return plannedList
                .Where(x => x.ProductToPartId == null)
                .GroupBy(x => x.VersionId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Planned));
        }
    
    public Dictionary<int, int> BuildDetailedInProgressByVersion(
    List<BatchPlannedRow> plannedList)
{
    return plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(
            g => g.Key,
            g => g.Sum(x => x.DetailedInProgress)
        );
}

public Dictionary<int, int> BuildDetailedFinishByVersion(
    List<BatchPlannedRow> plannedList)
{
    return plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(
            g => g.Key,
            g => g.Sum(x => x.DetailedFinish)
        );
}

public Dictionary<int, int> BuildAssemblyInProgressByVersion(
    List<BatchPlannedRow> plannedList)
{
    return plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(
            g => g.Key,
            g => g.Sum(x => x.AssemblyInProgress)
        );
}

public Dictionary<int, int> BuildAssemblyFinishByVersion(
    List<BatchPlannedRow> plannedList)
{
    return plannedList
        .GroupBy(x => x.VersionId)
        .ToDictionary(
            g => g.Key,
            g => g.Sum(x => x.AssemblyFinish)
        );
}

public async Task<List<OrderRalDto>> LoadOrdersAsync()
{
    return await _http.GetFromJsonAsync<List<OrderRalDto>>(
        "http://localhost:5270/api/orders/planning-orders")
        ?? new List<OrderRalDto>();
}

}