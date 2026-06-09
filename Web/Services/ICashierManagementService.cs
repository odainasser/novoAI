using Web.Models.Cashiers;
using Web.Models.Common;

namespace Web.Services;

public interface ICashierManagementService
{
    Task<PaginatedList<CashierResponse>> GetAllCashiersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseId = null);
    Task<CashierResponse?> GetCashierByIdAsync(Guid cashierId);
    Task<CashierResponse?> GetCurrentCashierProfileAsync();
    Task<CashierResponse> CreateCashierAsync(CreateCashierRequest request);
    Task<CashierResponse> UpdateCashierAsync(Guid cashierId, UpdateCashierRequest request);
    Task DeleteCashierAsync(Guid cashierId);
    Task<bool> CheckEmailExistsAsync(string email);
    Task<CashierResponse> SwitchMyStoreAsync(Guid warehouseId);
    Task<List<AssignedWarehouseDto>> GetMyAssignedStoresAsync();
}
