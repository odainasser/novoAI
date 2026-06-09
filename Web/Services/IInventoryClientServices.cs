using Web.Models.Common;
using Web.Models.Inventory;

namespace Web.Services;

public interface IGoodsReceivingClientService
{
    Task<PaginatedList<GoodsReceivingNoteDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? supplierId = null, Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null);
    Task<GoodsReceivingNoteDto?> GetByIdAsync(Guid id, Guid? branchId = null);
    Task<GoodsReceivingNoteDto> CreateAsync(CreateGoodsReceivingNoteRequest request);
    Task DeleteAsync(Guid id);
}

public interface IStockAdjustmentClientService
{
    Task<PaginatedList<StockAdjustmentDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, string? status = null, string? adjustmentType = null, Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null);
    Task<StockAdjustmentDto?> GetByIdAsync(Guid id, Guid? branchId = null);
    Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentRequest request);
    Task DeleteAsync(Guid id);
}

public interface IStockTransferClientService
{
    Task<PaginatedList<StockTransferDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? warehouseId = null, string? transferType = null, DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null);
    Task<StockTransferDto?> GetByIdAsync(Guid id, Guid? branchId = null);
    Task<StockTransferDto> CreateAsync(CreateStockTransferRequest request);
    Task DeleteAsync(Guid id);
}

public interface IStocktakeClientService
{
    Task<PaginatedList<StocktakeDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, string? type = null, string? status = null, Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null);
    Task<StocktakeDto?> GetByIdAsync(Guid id, Guid? branchId = null);
    Task<StocktakeDto> CreateAsync(CreateStocktakeRequest request, Guid? branchId = null);
    Task<StocktakeDto> StartAsync(Guid id, Guid? branchId = null);
    Task<StocktakeDto> SaveCountsAsync(Guid id, SaveStocktakeCountsRequest request, Guid? branchId = null);
    Task<StocktakeDto> CompleteAsync(Guid id, Guid? branchId = null);
    Task<StocktakeDto> ApproveAsync(Guid id, ApproveStocktakeRequest request, Guid? branchId = null);
    Task<StocktakeDto> CancelAsync(Guid id, Guid? branchId = null);
}

public interface IInventoryHistoryClientService
{
    Task<PaginatedList<InventoryHistoryDto>> GetAllAsync(int pageNumber, int pageSize, Guid? warehouseId = null, Guid? unitId = null, string? actionType = null, DateTime? fromDate = null, DateTime? toDate = null, string? referenceType = null, Guid? branchId = null);
    Task<InventoryHistoryDto?> GetByIdAsync(Guid id);
    Task<List<StockBalanceDto>> GetStockBalancesAsync(Guid warehouseId, string? search = null);
    Task<PaginatedList<StockBalanceDto>> GetAllStockBalancesAsync(int pageNumber, int pageSize, string? search = null, Guid? warehouseId = null, string? stockStatus = null, Guid? branchId = null);
    Task<int> GetTotalAvailableBySearchAsync(string search);
}
