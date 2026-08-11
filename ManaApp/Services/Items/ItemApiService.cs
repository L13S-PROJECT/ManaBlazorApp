// ItemApiService.cs

using System.Net.Http.Json;
using ManaApp.Shared.DTOs.Items;


namespace ManaApp.Services.Items;

public class ItemApiService
{
    private readonly HttpClient _http;

    public ItemApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ItemSelectorDto>> LoadSelectorAsync()
    {
        return await _http.GetFromJsonAsync<List<ItemSelectorDto>>(
            "http://localhost:5270/api/items/selector")
            ?? new();
    }

    public async Task<List<ProductTopPartItemDto>> LoadProductTopPartItemsAsync(int productTopPartId)
        {
            return await _http.GetFromJsonAsync<List<ProductTopPartItemDto>>(
                $"http://localhost:5270/api/producttoppartitems/list?productToPartId={productTopPartId}")
                ?? new();
        }

    public async Task<ItemEditDto?> LoadItemAsync(int id)
        {
            return await _http.GetFromJsonAsync<ItemEditDto>(
                $"http://localhost:5270/api/items/{id}");
        }

    public async Task<List<ItemTypeDto>> LoadTypesAsync()
        {
            return await _http.GetFromJsonAsync<List<ItemTypeDto>>(
                "http://localhost:5270/api/items/types")
                ?? new();
        }

    public async Task<int> CreateAsync(ItemEditDto dto)
        {
            var response = await _http.PostAsJsonAsync(
                "http://localhost:5270/api/items", dto);

            if (!response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadAsStringAsync();
                    throw new Exception(message);
                }

            return await response.Content.ReadFromJsonAsync<int>();

        }

    public async Task UpdateAsync(ItemEditDto dto)
        {
            var response = await _http.PutAsJsonAsync(
                $"http://localhost:5270/api/items/{dto.Id}", dto);

            if (!response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadAsStringAsync();
                    throw new Exception(message);
                }
        }

    public async Task DeleteAsync(int id)
        {
            var response = await _http.DeleteAsync(
                $"http://localhost:5270/api/items/{id}");

            response.EnsureSuccessStatusCode();
        }

    public async Task<List<ItemListDto>> LoadListAsync()
        {
            return await _http.GetFromJsonAsync<List<ItemListDto>>(
                "http://localhost:5270/api/items/list")
                ?? new();
        }

    public ItemEditDto CreateNew()
        {
            return new ItemEditDto
            {
                IsActive = true
            };
        }

    public async Task<List<UnitDto>> LoadUnitsAsync()
        {
            return await _http.GetFromJsonAsync<List<UnitDto>>(
                "http://localhost:5270/api/items/units")
                ?? new();
        }

}