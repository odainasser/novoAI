using Web.Models.Common;
using Web.Models.Offline;
using Web.Models.Warehouses;
using Web.Services;

namespace Web.Offline;

// Cashier-aware decorator around IWarehouseService.
//
// Background: the Cashier role does not have warehouses.read permission, so any
// call to /api/warehouses returns 403. The existing CashierLayout uses
// GetAllWarehousesAsync only to resolve the active shift's branch name, and
// that data is already in the offline cache (stores object store, populated by
// the offline sync).
//
// This wrapper returns the cached store list synthesized as WarehouseDtos for
// methods cashier-side code actually uses, and falls through to the inner
// service for everything else (so the admin panel keeps working unchanged).
public class OfflineWarehouseService : IWarehouseService
{
    private readonly IWarehouseService _inner;
    private readonly IIndexedDbService _idb;
    private readonly OfflineNetworkMonitor _network;

    public OfflineWarehouseService(IWarehouseService inner, IIndexedDbService idb, OfflineNetworkMonitor network)
    {
        _inner = inner;
        _idb = idb;
        _network = network;
    }

    public async Task<PaginatedList<WarehouseDto>> GetAllWarehousesAsync(
        int pageNumber, int pageSize, string? search = null, bool? isActive = null,
        Guid? warehouseTypeId = null, Guid? branchId = null)
    {
        // Online → server is the source of truth (admin reads need fresh data
        // and the cashier cache only contains MS stores, never CW). On 403 the
        // inner call throws and we fall through to the cache, which is the
        // cashier path.
        if (await _network.IsOnlineAsync())
        {
            try
            {
                return await _inner.GetAllWarehousesAsync(pageNumber, pageSize, search, isActive, warehouseTypeId, branchId);
            }
            catch
            {
                // fall through to cache
            }
        }

        var fromCache = await TryGetFromCacheAsync(pageNumber, pageSize, search, branchId);
        return fromCache ?? new PaginatedList<WarehouseDto>(new List<WarehouseDto>(), 0, pageNumber, pageSize);
    }

    public async Task<List<WarehouseDto>> GetActiveWarehousesAsync()
    {
        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.GetActiveWarehousesAsync(); }
            catch { /* cashier 403 or transient — fall back to cache */ }
        }

        var cached = await ReadCachedAsync();
        return cached.Select(MapToWarehouseDto).ToList();
    }

    public async Task<WarehouseDto?> GetWarehouseByIdAsync(Guid id)
    {
        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.GetWarehouseByIdAsync(id);
                if (live is not null) return live;
            }
            catch { /* fall through to cache */ }
        }

        var cached = await ReadCachedAsync();
        var hit = cached.FirstOrDefault(s => s.StoreId == id);
        return hit is not null ? MapToWarehouseDto(hit) : null;
    }

    // Mutations stay on the inner service — cashiers don't call them anyway.
    public Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request) => _inner.CreateWarehouseAsync(request);
    public Task<WarehouseDto> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request) => _inner.UpdateWarehouseAsync(id, request);
    public Task DeleteWarehouseAsync(Guid id) => _inner.DeleteWarehouseAsync(id);
    public Task<bool> CheckWarehouseExistsAsync(string nameEn, string nameAr, Guid? excludeWarehouseId = null) => _inner.CheckWarehouseExistsAsync(nameEn, nameAr, excludeWarehouseId);
    public Task<bool> CheckCentralWarehouseExistsAsync(Guid? excludeWarehouseId = null) => _inner.CheckCentralWarehouseExistsAsync(excludeWarehouseId);

    // ---------------- helpers ----------------

    private async Task<PaginatedList<WarehouseDto>?> TryGetFromCacheAsync(
        int pageNumber, int pageSize, string? search, Guid? branchId)
    {
        var cached = await ReadCachedAsync();
        if (cached.Count == 0) return null;

        IEnumerable<OfflineStoreDto> q = cached;
        if (branchId.HasValue)
        {
            // Cached stores carry the branch name but not its id; we can only
            // honour branchId when the inner service is reachable, so fall
            // through to the network in that case.
            return null;
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x =>
                (x.NameEn?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.NameAr?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = q.Select(MapToWarehouseDto).ToList();
        var total = list.Count;
        var page = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedList<WarehouseDto>(page, total, pageNumber, pageSize);
    }

    private async Task<List<OfflineStoreDto>> ReadCachedAsync()
    {
        try { return await _idb.GetAllAsync<OfflineStoreDto>(OfflineStores.Stores); }
        catch { return new List<OfflineStoreDto>(); }
    }

    private static WarehouseDto MapToWarehouseDto(OfflineStoreDto src) => new()
    {
        Id = src.StoreId,
        NameEn = src.NameEn,
        NameAr = src.NameAr,
        WarehouseTypeCode = src.Type,
        BranchNameEn = src.BranchNameEn,
        BranchNameAr = src.BranchNameAr,
        IsActive = true
    };
}
