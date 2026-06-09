using Application.Common.Models;
using Application.Features.Cashiers;

namespace Application.Services;

public interface ICashierService
{
    Task<PaginatedList<CashierResponse>> GetAllCashiersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseId = null, CancellationToken cancellationToken = default, IReadOnlyList<Guid>? warehouseIds = null);
    Task<CashierResponse?> GetCashierByIdAsync(Guid cashierId, CancellationToken cancellationToken = default);
    Task<CashierResponse> CreateCashierAsync(CreateCashierRequest request, CancellationToken cancellationToken = default);
    Task<CashierResponse> UpdateCashierAsync(Guid cashierId, UpdateCashierRequest request, CancellationToken cancellationToken = default);
    Task DeleteCashierAsync(Guid cashierId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CashierResponse>> GetActiveCashiersAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<CashierResponse> SwitchStoreAsync(Guid cashierId, Guid warehouseId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AssignedWarehouseDto>> GetAssignedStoresAsync(Guid cashierId, CancellationToken cancellationToken = default);
}
