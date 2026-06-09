using Application.Common.Models;
using Application.Features.Promotions;

namespace Application.Services;

public interface IPromotionService
{
    Task<PaginatedList<PromotionDto>> GetAllPromotionsAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null);
    Task<PromotionDto?> GetPromotionByIdAsync(Guid id);
    Task<PromotionDto> CreatePromotionAsync(CreatePromotionRequest request);
    Task<PromotionDto> UpdatePromotionAsync(Guid id, UpdatePromotionRequest request);
    Task DeletePromotionAsync(Guid id);
    Task<List<PromotionDto>> GetActivePromotionsAsync();
    Task<List<PromotionDto>> GetPromotionsForUnitAsync(Guid unitId);
}
