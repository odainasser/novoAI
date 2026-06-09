using Application.Common.Models;
using Application.Features.Inventory;

namespace Application.Services;

public interface IStockAdjustmentService
{
    Task<PaginatedList<StockAdjustmentDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, string? status = null, Guid? warehouseId = null, string? adjustmentType = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<StockAdjustmentDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Creates a stock adjustment and applies it immediately to StockBalance.
    /// When <paramref name="stocktakeId"/> is supplied the adjustment is tagged
    /// as stocktake-originated and the InventoryHistory entries it writes
    /// reference the stocktake (ReferenceType "Stocktake") instead of the
    /// adjustment. Existing callers pass null and keep the original behaviour.
    /// </summary>
    Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentRequest request, Guid? stocktakeId = null);
    Task DeleteAsync(Guid id);
}
