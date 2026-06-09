using Application.Common.Models;
using Application.Features.Inventory;

namespace Application.Services;

public interface IStockTransferService
{
    Task<PaginatedList<StockTransferDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? warehouseId = null, string? transferType = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<StockTransferDto?> GetByIdAsync(Guid id);
    Task<StockTransferDto> CreateAsync(CreateStockTransferRequest request);
    Task DeleteAsync(Guid id);
}
