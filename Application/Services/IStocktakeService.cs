using Application.Common.Models;
using Application.Features.Inventory;

namespace Application.Services;

public interface IStocktakeService
{
    Task<PaginatedList<StocktakeDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null, string? type = null, string? status = null,
        Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null);

    Task<StocktakeDto?> GetByIdAsync(Guid id);

    /// <summary>Creates the header in Draft.</summary>
    Task<StocktakeDto> CreateAsync(CreateStocktakeRequest request);

    /// <summary>Snapshots the in-scope units' current quantities into lines and moves to InProgress.</summary>
    Task<StocktakeDto> StartAsync(Guid id);

    /// <summary>Progressively saves counted quantities (stays InProgress). Never touches StockBalance.</summary>
    Task<StocktakeDto> SaveCountsAsync(Guid id, SaveStocktakeCountsRequest request);

    /// <summary>Finalises counting, flags matched/differing lines, notifies reviewers, moves to Completed.</summary>
    Task<StocktakeDto> CompleteAsync(Guid id);

    /// <summary>Applies the per-line adjustment types, generates adjustments, moves to Approved.</summary>
    Task<StocktakeDto> ApproveAsync(Guid id, ApproveStocktakeRequest request);

    /// <summary>Abandons the stocktake. StockBalance is never touched.</summary>
    Task<StocktakeDto> CancelAsync(Guid id);
}
