using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.PurchaseRequests;

namespace Web.Services;

public interface IPurchaseRequestClientService
{
    Task<PaginatedList<PurchaseRequestDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null,
        PurchaseRequestStatus? status = null, PurchaseRequestSupplySource? supplySource = null,
        Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null, Guid? branchId = null);

    Task<PurchaseRequestDto?> GetByIdAsync(Guid id, Guid? branchId = null);

    Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request, Guid? branchId = null);
    Task<PurchaseRequestDto?> UpdateAsync(Guid id, UpdatePurchaseRequestRequest request, Guid? branchId = null);

    Task<int> GenerateAutoReorderProposalsAsync(Guid? warehouseId = null, Guid? branchId = null);

    Task<PurchaseRequestDto?> SubmitAsync(Guid id, Guid? branchId = null);
    Task<PurchaseRequestDto?> ApproveAsync(Guid id, string? note = null);
    Task<PurchaseRequestDto?> RejectAsync(Guid id, string? reason = null);
    Task<PurchaseRequestDto?> ConvertAsync(Guid id);
    Task<PurchaseRequestDto?> CancelAsync(Guid id, Guid? branchId = null);
}
