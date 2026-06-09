using Web.Models.Common;
using Web.Models.Warehouses;

namespace Web.Services;

public interface IWarehouseService
{
    Task<PaginatedList<WarehouseDto>> GetAllWarehousesAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseTypeId = null, Guid? branchId = null);
    Task<List<WarehouseDto>> GetActiveWarehousesAsync();
    Task<WarehouseDto?> GetWarehouseByIdAsync(Guid id);
    Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request);
    Task<WarehouseDto> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request);
    Task DeleteWarehouseAsync(Guid id);
    Task<bool> CheckWarehouseExistsAsync(string nameEn, string nameAr, Guid? excludeWarehouseId = null);
    Task<bool> CheckCentralWarehouseExistsAsync(Guid? excludeWarehouseId = null);
}
