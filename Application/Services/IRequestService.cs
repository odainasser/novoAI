using Application.Common.Models;
using Application.Features.Requests;
using Domain.Enums;

namespace Application.Services;

public interface IRequestService
{
    Task<PaginatedList<RequestDto>> GetAllRequestsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        RequestType? type = null,
        RequestStatus? status = null);

    // Branch Panel: requests submitted by a set of users (the branch's members).
    Task<PaginatedList<RequestDto>> GetByRequesterIdsAsync(
        IEnumerable<Guid> requesterIds,
        int pageNumber,
        int pageSize,
        RequestStatus? status = null);

    Task<RequestDto?> GetRequestByIdAsync(Guid id);
    Task<RequestDto?> GetPendingProductUpdateRequestAsync(Guid productId);

    Task<RequestDto> CreateSetUnitPriceRequestAsync(CreateSetUnitPriceRequest request);

    Task<RequestDto> CreateActivateProductRequestAsync(CreateActivateProductRequest request);

    Task<RequestDto> CreateActivateUnitRequestAsync(CreateActivateUnitRequest request);

    Task<RequestDto?> ReviewRequestAsync(Guid id, ReviewRequestDto review);

    Task<RequestDto> CreateAddGRNRequestAsync(CreateInventoryGRNRequest request);
    Task<RequestDto?> UpdateAddGRNRequestAsync(Guid id, CreateInventoryGRNRequest request);
    Task<RequestDto> CreateAddStockAdjustmentRequestAsync(CreateInventoryAdjustmentRequest request);
    Task<RequestDto> CreateAddStockTransferRequestAsync(CreateInventoryTransferRequest request);
    Task<RequestDto> CreateDeleteProductRequestAsync(CreateDeleteProductRequest request);
    Task<RequestDto> CreateDeleteUnitRequestAsync(CreateDeleteUnitRequest request);
    Task<RequestDto> CreateSetLogisticsDetailsRequestAsync(CreateSetLogisticsDetailsRequest request);
    Task<RequestDto?> GetPendingSetLogisticsDetailsRequestAsync(Guid unitId);

    Task<PaginatedList<RequestDto>> GetMyRequestsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        RequestType? type = null,
        RequestStatus? status = null);

    Task<bool> DeleteRequestAsync(Guid id);
    Task<bool> DeleteMyRequestAsync(Guid id);
}
