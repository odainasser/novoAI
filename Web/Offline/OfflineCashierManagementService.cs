using Web.Models.Cashiers;
using Web.Models.Common;
using Web.Models.Offline;
using Web.Services;

namespace Web.Offline;

// Offline-aware decorator around ICashierManagementService. The cashier panel
// reads the profile + assigned stores from this service and calls
// SwitchMyStoreAsync from StartShiftModal. Without this wrapper:
//   - GetCurrentCashierProfileAsync throws while offline → modal shows no stores
//   - SwitchMyStoreAsync 400s when the cashier ended their shift offline (the
//     EndShift queue item hasn't been replayed yet, so the server still sees
//     an active shift and rejects the switch)
//
// The wrapper serves both calls from the offline cache when the network is
// unreachable (or the call fails), and updates ActiveStoreContext so the rest
// of the offline layer routes data through the new store.
public class OfflineCashierManagementService : ICashierManagementService
{
    private readonly ICashierManagementService _inner;
    private readonly IIndexedDbService _idb;
    private readonly ActiveStoreContext _activeStore;
    private readonly OfflineNetworkMonitor _network;

    public OfflineCashierManagementService(
        ICashierManagementService inner,
        IIndexedDbService idb,
        ActiveStoreContext activeStore,
        OfflineNetworkMonitor network)
    {
        _inner = inner;
        _idb = idb;
        _activeStore = activeStore;
        _network = network;
    }

    public async Task<CashierResponse?> GetCurrentCashierProfileAsync()
    {
        // Rehydrate the active store before any offline fallback so a hard
        // reload sees the switched store, not the first-assigned one.
        await _activeStore.EnsureLoadedAsync();

        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.GetCurrentCashierProfileAsync();
                if (live is not null) return live;
            }
            catch { /* fall through */ }
        }

        return await BuildOfflineProfileAsync();
    }

    public async Task<List<AssignedWarehouseDto>> GetMyAssignedStoresAsync()
    {
        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.GetMyAssignedStoresAsync();
                if (live.Count > 0) return live;
            }
            catch { /* fall through */ }
        }

        return (await ReadCachedStoresAsync())
            .Select(s => new AssignedWarehouseDto { Id = s.StoreId, NameEn = s.NameEn, NameAr = s.NameAr })
            .ToList();
    }

    public async Task<CashierResponse> SwitchMyStoreAsync(Guid warehouseId)
    {
        // Online: hit the server first. If it rejects because of a pending
        // offline EndShift that hasn't replayed yet, fall through and switch
        // locally — the server will catch up when the queue flushes.
        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.SwitchMyStoreAsync(warehouseId);
                ApplyToActiveStoreAsync(live).GetAwaiter().GetResult();
                return live;
            }
            catch { /* fall through to local switch */ }
        }

        return await SwitchLocallyAsync(warehouseId);
    }

    // Admin-only paths — pass straight through.
    public Task<PaginatedList<CashierResponse>> GetAllCashiersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseId = null)
        => _inner.GetAllCashiersAsync(pageNumber, pageSize, search, isActive, warehouseId);

    public Task<CashierResponse?> GetCashierByIdAsync(Guid cashierId) => _inner.GetCashierByIdAsync(cashierId);
    public Task<CashierResponse> CreateCashierAsync(CreateCashierRequest request) => _inner.CreateCashierAsync(request);
    public Task<CashierResponse> UpdateCashierAsync(Guid cashierId, UpdateCashierRequest request) => _inner.UpdateCashierAsync(cashierId, request);
    public Task DeleteCashierAsync(Guid cashierId) => _inner.DeleteCashierAsync(cashierId);
    public Task<bool> CheckEmailExistsAsync(string email) => _inner.CheckEmailExistsAsync(email);

    // ---------------- internals ----------------

    private async Task<CashierResponse?> BuildOfflineProfileAsync()
    {
        OfflineProfileDto? profile = null;
        OfflineCredentialDto? credential = null;
        List<OfflineStoreDto> stores;
        try
        {
            var profiles = await _idb.GetAllAsync<OfflineProfileDto>(OfflineStores.Profile);
            profile = profiles.FirstOrDefault();
            var creds = await _idb.GetAllAsync<OfflineCredentialDto>(OfflineStores.Credential);
            credential = creds.FirstOrDefault();
            stores = await _idb.GetAllAsync<OfflineStoreDto>(OfflineStores.Stores);
        }
        catch
        {
            return null;
        }

        if (profile is null && credential is null) return null;

        var firstStoreId = _activeStore.StoreId ?? credential?.AssignedStoreIds.FirstOrDefault();
        var activeStore = firstStoreId.HasValue ? stores.FirstOrDefault(s => s.StoreId == firstStoreId.Value) : null;

        return new CashierResponse
        {
            Id = profile?.UserId ?? credential?.UserId ?? Guid.Empty,
            Email = profile?.Email ?? credential?.Email ?? string.Empty,
            FirstName = profile?.FirstName,
            LastName = profile?.LastName,
            FullName = profile?.DisplayName ?? string.Empty,
            IsActive = true,
            CanRefund = profile?.CanRefund ?? false,
            WarehouseId = firstStoreId,
            WarehouseNameEn = activeStore?.NameEn,
            WarehouseNameAr = activeStore?.NameAr,
            AssignedWarehouses = stores
                .Where(s => credential is null
                            || credential.AssignedStoreIds.Count == 0
                            || credential.AssignedStoreIds.Contains(s.StoreId))
                .Select(s => new AssignedWarehouseDto { Id = s.StoreId, NameEn = s.NameEn, NameAr = s.NameAr })
                .ToList()
        };
    }

    private async Task<CashierResponse> SwitchLocallyAsync(Guid warehouseId)
    {
        var stores = await ReadCachedStoresAsync();
        var match = stores.FirstOrDefault(s => s.StoreId == warehouseId);
        _activeStore.Set(warehouseId, match?.NameEn, match?.NameAr);

        var profile = await BuildOfflineProfileAsync() ?? new CashierResponse { Id = Guid.Empty, IsActive = true };
        profile.WarehouseId = warehouseId;
        profile.WarehouseNameEn = match?.NameEn;
        profile.WarehouseNameAr = match?.NameAr;
        return profile;
    }

    private async Task<List<OfflineStoreDto>> ReadCachedStoresAsync()
    {
        try { return await _idb.GetAllAsync<OfflineStoreDto>(OfflineStores.Stores); }
        catch { return new List<OfflineStoreDto>(); }
    }

    private async Task ApplyToActiveStoreAsync(CashierResponse response)
    {
        if (!response.WarehouseId.HasValue) return;
        var stores = await ReadCachedStoresAsync();
        var match = stores.FirstOrDefault(s => s.StoreId == response.WarehouseId.Value);
        _activeStore.Set(response.WarehouseId.Value,
            match?.NameEn ?? response.WarehouseNameEn,
            match?.NameAr ?? response.WarehouseNameAr);
    }
}
