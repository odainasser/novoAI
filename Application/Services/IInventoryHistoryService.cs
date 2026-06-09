using Application.Common.Models;
using Application.Features.Inventory;

namespace Application.Services;

public interface IInventoryHistoryService
{
    Task<PaginatedList<InventoryHistoryDto>> GetAllAsync(int pageNumber, int pageSize, Guid? warehouseId = null, Guid? unitId = null, string? actionType = null, DateTime? fromDate = null, DateTime? toDate = null, string? referenceType = null, IEnumerable<Guid>? warehouseIds = null);
    Task<InventoryHistoryDto?> GetByIdAsync(Guid id);
    Task<List<StockBalanceDto>> GetStockBalancesAsync(Guid warehouseId, string? search = null);
    Task<PaginatedList<StockBalanceDto>> GetAllStockBalancesAsync(int pageNumber, int pageSize, string? search = null, Guid? warehouseId = null, string? stockStatus = null, IReadOnlyList<Guid>? warehouseIds = null);
    Task<int> GetTotalAvailableBySearchAsync(string search);
}
