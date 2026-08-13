using System.Net.Http.Json;
using ManaApp.Shared.DTOs.Items;

namespace ManaApp.Services
{
    public class ItemService
    {
        private readonly HttpClient _http;

        public ItemService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<ItemSelectorDto>> GetSelectorAsync()
        {
            return await _http.GetFromJsonAsync<List<ItemSelectorDto>>(
                "api/Items/selector") ?? new();
        }
    }
}