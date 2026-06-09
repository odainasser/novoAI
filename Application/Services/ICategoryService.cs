using Application.Common.Models;
using Application.Features.Categories;

namespace Application.Services;

public interface ICategoryService
{
    Task<PaginatedList<CategoryDto>> GetAllCategoriesAsync(int pageNumber, int pageSize, Guid? parentId = null, string? search = null, bool? isActive = null);
    Task<List<CategoryDto>> GetRootCategoriesAsync();
    Task<List<CategoryTreeDto>> GetCategoryTreeAsync();
    Task<CategoryDto?> GetCategoryByIdAsync(Guid id);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request);
    Task DeleteCategoryAsync(Guid id);
    Task<bool> CheckCategoryExistsAsync(string nameEn, string nameAr, Guid? excludeCategoryId = null);
}
