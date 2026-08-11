//itemApiService.cs

using ManaApp.Shared.DTOs.Items;

namespace ManaApp.Services.Items;

public class ItemsStateService
{
    private readonly ItemApiService _itemApi;

    public ItemsStateService(ItemApiService itemApi)
    {
        _itemApi = itemApi;
    }

    public event Action? OnChange;

    public List<ItemListDto> Items { get; set; } = new();

    public ItemEditDto? SelectedItem { get; set; }

    public int? SelectedItemId { get; private set; }

    public void SelectItem(int id)
    {
        SelectedItemId = id;
        OnChange?.Invoke();
    }

    public void ClearSelection()
        {
            SelectedItemId = null;
            OnChange?.Invoke();
        }

    public async Task ReloadAsync()
        {
            Items = await _itemApi.LoadListAsync();
            OnChange?.Invoke();
        }


}