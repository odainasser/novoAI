using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Products;

namespace Web.Services;

public interface IProductService
{
    Task<PaginatedList<ProductDto>> GetAllProductsAsync(int pageNumber, int pageSize, string? search = null, Guid? categoryId = null, bool? isActive = null, Guid? warehouseId = null, bool? onlyWithStock = null, ItemStatus? status = null);
    Task<ProductDetailDto?> GetProductByIdAsync(Guid id);
    Task<ProductDto?> GetProductByCodeAsync(string code);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request);
    Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request);
    Task DeleteProductAsync(Guid id);
    Task<bool> CheckCodeExistsAsync(string code, Guid? excludeProductId = null);
}
