using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Requests;

namespace Web.Services;

public interface IRequestService
{
    Task<PaginatedList<RequestDto>> GetAllRequestsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        RequestType? type = null,
        RequestStatus? status = null,
        Guid? branchId = null);

    Task<RequestDto?> GetRequestByIdAsync(Guid id);
    Task<RequestDto?> GetPendingProductUpdateRequestAsync(Guid productId);
    Task<RequestDto> CreateSetUnitPriceRequestAsync(CreateSetUnitPriceRequest request);
    Task<RequestDto> CreateActivateProductRequestAsync(CreateActivateProductRequest request);
    Task<RequestDto> CreateActivateUnitRequestAsync(CreateActivateUnitRequest request);
    Task<RequestDto?> ReviewRequestAsync(Guid id, ReviewRequestDto review);
    Task<RequestDto> CreateAddGRNRequestAsync(CreateInventoryGRNRequest request, Guid? branchId = null);
    Task<RequestDto?> UpdateAddGRNRequestAsync(Guid id, CreateInventoryGRNRequest request);
    Task<RequestDto> CreateAddStockAdjustmentRequestAsync(CreateInventoryAdjustmentRequest request, Guid? branchId = null);
    Task<RequestDto> CreateAddStockTransferRequestAsync(CreateInventoryTransferRequest request, Guid? branchId = null);
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
