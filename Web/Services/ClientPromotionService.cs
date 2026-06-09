using System.Net.Http.Json;
using Web.Models.Common;
using Web.Models.Promotions;

namespace Web.Services;

public class ClientPromotionService : IPromotionService
{
    private readonly HttpClient _httpClient;

    public ClientPromotionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaginatedList<PromotionDto>> GetAllPromotionsAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null)
    {
        var url = $"api/promotions?pageNumber={pageNumber}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        if (isActive.HasValue)
        {
            url += $"&isActive={isActive.Value}";
        }

        return await _httpClient.GetFromJsonAsync<PaginatedList<PromotionDto>>(url)
               ?? new PaginatedList<PromotionDto>(new List<PromotionDto>(), 0, pageNumber, pageSize);
    }

    public async Task<PromotionDto?> GetPromotionByIdAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PromotionDto>($"api/promotions/{id}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PromotionDto> CreatePromotionAsync(CreatePromotionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/promotions", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<PromotionDto>() ?? throw new Exception("Failed to create promotion");
    }

    public async Task<PromotionDto> UpdatePromotionAsync(Guid id, UpdatePromotionRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/promotions/{id}", request);
        await response.HandleErrorAsync();
        return await response.Content.ReadFromJsonAsync<PromotionDto>() ?? throw new Exception("Failed to update promotion");
    }

    public async Task DeletePromotionAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"api/promotions/{id}");
        await response.HandleErrorAsync();
    }

    public async Task<List<PromotionDto>> GetActivePromotionsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<PromotionDto>>("api/promotions/active")
               ?? new List<PromotionDto>();
    }

    public async Task<List<PromotionDto>> GetPromotionsForUnitAsync(Guid unitId)
    {
        return await _httpClient.GetFromJsonAsync<List<PromotionDto>>($"api/promotions/unit/{unitId}")
               ?? new List<PromotionDto>();
    }
}
