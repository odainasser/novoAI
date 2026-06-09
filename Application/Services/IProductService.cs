using Application.Common.Models;
using Application.Features.Products;

namespace Application.Services;

public interface IProductService
{
    Task<PaginatedList<ProductDto>> GetAllProductsAsync(int pageNumber, int pageSize, string? search = null, Guid? categoryId = null, bool? isActive = null, Guid? warehouseId = null, bool? onlyWithStock = null, Domain.Enums.ItemStatus? status = null);
    Task<ProductDetailDto?> GetProductByIdAsync(Guid id);
    Task<ProductDto?> GetProductByCodeAsync(string code);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request);
    Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request);
    Task DeleteProductAsync(Guid id);
    Task<bool> CheckCodeExistsAsync(string code, Guid? excludeProductId = null);
}
