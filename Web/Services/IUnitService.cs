using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Units;

namespace Web.Services;

public interface IUnitService
{
    Task<PaginatedList<UnitDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? productId = null, Guid? unitOfMeasureId = null, bool? isActive = null, Guid? unitTypeId = null, Guid? categoryId = null, Guid? supplierId = null, ItemStatus? status = null);
    Task<UnitDto?> GetByIdAsync(Guid id);
    Task<UnitDto> CreateAsync(CreateUnitRequest request);
    Task<UnitDto> UpdateAsync(Guid id, UpdateUnitRequest request);
    Task DeleteAsync(Guid id);
    Task<UnitDto> SetSellingDetailsAsync(Guid id, SetSellingDetailsRequest request);
    Task<UnitDto> SetLogisticsDetailsAsync(Guid id, SetLogisticsDetailsRequest request);
}
