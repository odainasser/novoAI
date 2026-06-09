using Application.Common.Models;
using Application.Features.PurchaseRequests;
using Domain.Enums;

namespace Application.Services;

public interface IPurchaseRequestService
{
    Task<PaginatedList<PurchaseRequestDto>> GetAllAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        PurchaseRequestStatus? status = null,
        PurchaseRequestSupplySource? supplySource = null,
        Guid? warehouseId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null);

    /// <summary>Branch Panel: list purchase requests for a specific set of warehouses.</summary>
    Task<PaginatedList<PurchaseRequestDto>> GetByWarehouseIdsAsync(
        IEnumerable<Guid> warehouseIds,
        int pageNumber,
        int pageSize,
        PurchaseRequestStatus? status = null,
        PurchaseRequestSupplySource? supplySource = null);

    Task<PurchaseRequestDto?> GetByIdAsync(Guid id);

    Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request);
    Task<PurchaseRequestDto?> UpdateAsync(Guid id, UpdatePurchaseRequestRequest request);

    /// <summary>Scan stock balances and create draft auto-reorder proposals. Returns the number of drafts created. Never auto-submits.</summary>
    Task<int> GenerateAutoReorderProposalsAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto?> SubmitAsync(Guid id);
    Task<PurchaseRequestDto?> ApproveAsync(Guid id, string? note = null);
    Task<PurchaseRequestDto?> RejectAsync(Guid id, string? reason = null);
    Task<PurchaseRequestDto?> ConvertAsync(Guid id);
    Task<PurchaseRequestDto?> CancelAsync(Guid id);
}
