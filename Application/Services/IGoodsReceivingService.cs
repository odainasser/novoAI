using Application.Common.Models;
using Application.Features.Inventory;

namespace Application.Services;

public interface IGoodsReceivingService
{
    Task<PaginatedList<GoodsReceivingNoteDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? supplierId = null, Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<GoodsReceivingNoteDto?> GetByIdAsync(Guid id);
    Task<GoodsReceivingNoteDto> CreateAsync(CreateGoodsReceivingNoteRequest request);
    Task DeleteAsync(Guid id);
}
