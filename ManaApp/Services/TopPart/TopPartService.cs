using System.Net.Http.Json;
using ManaApp.Shared.DTOs.TopPart;

namespace ManaApp.Services
{
    public class TopPartService
    {
        private readonly HttpClient _http;
        

        public TopPartService(HttpClient http)
        {
            _http = http;
        }

        public string? LastError { get; private set; }

        public async Task<List<TopPartCategoryDto>> GetCategoriesAsync()
            {
                return await _http.GetFromJsonAsync<List<TopPartCategoryDto>>(
                    "api/TopPartCategories") ?? new List<TopPartCategoryDto>();
            }

        public async Task<List<TopPartGroupCategoryDto>> GetGroupCategoriesAsync()
            {
                return await _http.GetFromJsonAsync<List<TopPartGroupCategoryDto>>(
                    "api/Categories") ?? new List<TopPartGroupCategoryDto>();
            }
        
        public async Task<TopPartListItemDto?> CreateAsync(CreateTopPartDto model)
            {
                LastError = null;

                var response = await _http.PostAsJsonAsync("api/TopParts", model);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<TopPartListItemDto>();
            }

        public async Task<List<TopPartListItemDto>> GetAllAsync(
            byte? type = null,
            int? categoryId = null,
            string? search = null,
            uint? relatedTopPartId = null)
            {
                var parameters = new List<string>();

                    if (type.HasValue)
                        parameters.Add($"type={type.Value}");

                    if (categoryId.HasValue)
                        parameters.Add($"categoryId={categoryId.Value}");
                    
                    if (!string.IsNullOrWhiteSpace(search))
                        parameters.Add($"search={Uri.EscapeDataString(search.Trim())}");

                    if (relatedTopPartId.HasValue)
                        parameters.Add($"relatedTopPartId={relatedTopPartId.Value}");

                var url = parameters.Count > 0
                    ? $"api/TopParts?{string.Join("&", parameters)}"
                    : "api/TopParts";

                return await _http.GetFromJsonAsync<List<TopPartListItemDto>>(url)
                    ?? new List<TopPartListItemDto>();
            }

        public async Task<TopPartListItemDto?> UpdateAsync(UpdateTopPartDto model)
            {
                LastError = null;

                var response = await _http.PutAsJsonAsync("api/TopParts", model);

                if (!response.IsSuccessStatusCode)
                {
                    LastError = await response.Content.ReadAsStringAsync();
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<TopPartListItemDto>();
            }

        public async Task<TopPartUsageDto> GetUsageAsync(int topPartId)
            {
                return await _http.GetFromJsonAsync<TopPartUsageDto>(
                    $"api/TopParts/{topPartId}/usage")
                    ?? new TopPartUsageDto();
            }

    }
}