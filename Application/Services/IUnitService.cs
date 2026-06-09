using Application.Common.Models;
using Application.Features.Units;

namespace Application.Services;

public interface IUnitService
{
    Task<PaginatedList<UnitDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? productId = null, Guid? unitOfMeasureId = null, bool? isActive = null, Guid? unitTypeId = null, Guid? categoryId = null, Guid? supplierId = null, Domain.Enums.ItemStatus? status = null);
    Task<UnitDto?> GetByIdAsync(Guid id);
    Task<UnitDto> CreateAsync(CreateUnitRequest request);
    Task<UnitDto> UpdateAsync(Guid id, UpdateUnitRequest request);
    Task DeleteAsync(Guid id);
    Task<bool> CheckBarcodeExistsAsync(string barcode, Guid? excludeId = null);
    Task<UnitDto> SetSellingDetailsAsync(Guid id, decimal sellingPrice, string sellingBarcode, int lowStockThreshold);
    Task<UnitDto> SetLogisticsDetailsAsync(Guid id, decimal cost, List<UnitSupplierItem> suppliers, int lowStockThreshold);
}
