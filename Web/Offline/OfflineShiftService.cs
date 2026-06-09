using System.Text.Json;
using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Offline;
using Web.Models.Shifts;
using Web.Services;

namespace Web.Offline;

// Offline-aware decorator around IShiftService. Same pattern as the order
// wrapper: reads come from IndexedDB when available, writes go to IDB + the
// sync queue first, then to the server if online.
public class OfflineShiftService : IShiftService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IShiftService _inner;
    private readonly IIndexedDbService _idb;
    private readonly ActiveStoreContext _activeStore;
    private readonly OfflineNetworkMonitor _network;
    private readonly IOfflineSyncService _sync;

    public OfflineShiftService(
        IShiftService inner,
        IIndexedDbService idb,
        ActiveStoreContext activeStore,
        OfflineNetworkMonitor network,
        IOfflineSyncService sync)
    {
        _inner = inner;
        _idb = idb;
        _activeStore = activeStore;
        _network = network;
        _sync = sync;
    }

    public async Task<ShiftDto> StartShiftAsync(StartShiftRequest request)
    {
        // Ensure the active store has been rehydrated from localStorage so a
        // hard reload (offline) doesn't stamp the shift with the cashier's
        // first-assigned store instead of the switched one.
        await _activeStore.EnsureLoadedAsync();

        // Online: hit the server directly so the queue stays clean. Enqueuing
        // here as well would cause a duplicate replay later (the server rejects
        // a second start with "An active shift already exists").
        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.StartShiftAsync(request);
                if (live is not null)
                {
                    try { await _idb.UpsertAsync(OfflineStores.Shifts, MapServerShiftToOffline(live)); } catch { }
                    return live;
                }
            }
            catch { /* server unreachable — fall through to offline path so the shift isn't lost */ }
        }

        // Offline (or live call failed): write a local placeholder + queue it.
        // Fill in the store name from the cached store list if the active store
        // context wasn't populated (cashier didn't visit StoreSelector first),
        // so the header has something to show right away.
        // Stamp ClientStartedAt so the server records the actual open time on replay.
        var startTime = DateTime.UtcNow;
        request.ClientStartedAt = startTime;

        var localId = Guid.NewGuid();
        var (storeId, storeNameEn, storeNameAr) = await ResolveStoreForLocalShiftAsync();
        var local = new OfflineShiftDto
        {
            ShiftId = localId,
            StoreId = storeId,
            StoreNameEn = storeNameEn,
            StoreNameAr = storeNameAr,
            StartTime = startTime,
            CashIn = request.CashIn,
            Status = ShiftStatus.Active.ToString(),
            Comments = request.Comments
        };
        try
        {
            await _idb.UpsertAsync(OfflineStores.Shifts, local);
            await _sync.EnqueueAsync(new SyncQueueItem
            {
                Op = SyncQueueOpType.StartShift,
                StoreId = _activeStore.StoreId,
                TargetId = localId,
                PayloadJson = JsonSerializer.Serialize(new QueuedStartShift
                {
                    LocalShiftId = localId,
                    StoreId = _activeStore.StoreId,
                    Request = request
                }, _json)
            });
        }
        catch { }

        return MapToShiftDto(local);
    }

    public async Task<ShiftDto> EndShiftAsync(Guid id, EndShiftRequest request)
    {
        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.EndShiftAsync(id, request);
                if (live is not null)
                {
                    try { await _idb.UpsertAsync(OfflineStores.Shifts, MapServerShiftToOffline(live)); } catch { }
                    return live;
                }
            }
            catch { /* fall through */ }
        }

        var local = await TryGetCachedShiftAsync(id);
        if (local is null)
            throw new InvalidOperationException("Cannot end an uncached shift while offline.");

        // Stamp ClientEndedAt so the server records the actual close time on replay.
        var endTime = DateTime.UtcNow;
        request.ClientEndedAt = endTime;

        local.EndTime = endTime;
        local.CashOut = request.CashOut;
        local.Comments = request.Comments;
        local.Status = ShiftStatus.Completed.ToString();
        try { await _idb.UpsertAsync(OfflineStores.Shifts, local); } catch { }

        await _sync.EnqueueAsync(new SyncQueueItem
        {
            Op = SyncQueueOpType.EndShift,
            StoreId = local.StoreId,
            TargetId = id,
            PayloadJson = JsonSerializer.Serialize(new QueuedEndShift { ShiftId = id, Request = request }, _json)
        });

        return MapToShiftDto(local);
    }

    public async Task<PaginatedList<ShiftDto>> GetMyShiftsAsync(int pageNumber = 1, int pageSize = 10, Guid? warehouseId = null)
    {
        await _activeStore.EnsureLoadedAsync();

        // Online: live data wins. Cache is read-only fallback for offline mode.
        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.GetMyShiftsAsync(pageNumber, pageSize, warehouseId); }
            catch { /* fall through to cache */ }
        }

        try
        {
            var cached = await _idb.GetAllAsync<OfflineShiftDto>(OfflineStores.Shifts);
            var scoped = warehouseId.HasValue
                ? cached.Where(s => s.StoreId == warehouseId.Value).ToList()
                : cached;

            var mapped = scoped
                .OrderByDescending(s => s.StartTime)
                .Select(MapToShiftDto)
                .ToList();

            var total = mapped.Count;
            var page = mapped.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return new PaginatedList<ShiftDto>(page, total, pageNumber, pageSize);
        }
        catch
        {
            return new PaginatedList<ShiftDto>(new List<ShiftDto>(), 0, pageNumber, pageSize);
        }
    }

    public Task<PaginatedList<ShiftDto>> GetAllShiftsAsync(int pageNumber = 1, int pageSize = 20, string? status = null, string? search = null, Guid? cashierId = null, Guid? warehouseId = null, Guid? branchId = null, DateTime? fromDate = null, DateTime? toDate = null)
        => _inner.GetAllShiftsAsync(pageNumber, pageSize, status, search, cashierId, warehouseId, branchId, fromDate, toDate);

    public async Task<bool> HasActiveShiftAsync()
    {
        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.HasActiveShiftAsync(); }
            catch { /* fall through to cache */ }
        }

        try
        {
            var cached = await _idb.GetAllAsync<OfflineShiftDto>(OfflineStores.Shifts);
            return cached.Any(s => s.Status == ShiftStatus.Active.ToString() || s.EndTime is null);
        }
        catch
        {
            return false;
        }
    }

    public Task<byte[]> ExportShiftsToExcelAsync(string? status = null, string? search = null, Guid? cashierId = null, Guid? warehouseId = null, bool isArabic = false, Guid? branchId = null)
        => _inner.ExportShiftsToExcelAsync(status, search, cashierId, warehouseId, isArabic, branchId);

    // ---------------- helpers ----------------

    private async Task<OfflineShiftDto?> TryGetCachedShiftAsync(Guid id)
    {
        try { return await _idb.GetByKeyAsync<OfflineShiftDto>(OfflineStores.Shifts, id); }
        catch { return null; }
    }

    // Pick the store for a shift that's being started offline. Priority order:
    //   1. ActiveStoreContext (cashier explicitly picked one)
    //   2. First assigned store from the cached credential
    //   3. Cached profile fallback — names looked up from cached stores
    // Returns (null, null, null) when nothing is available.
    private async Task<(Guid? storeId, string? nameEn, string? nameAr)> ResolveStoreForLocalShiftAsync()
    {
        if (_activeStore.StoreId.HasValue)
            return (_activeStore.StoreId, _activeStore.StoreNameEn, _activeStore.StoreNameAr);

        try
        {
            var creds = await _idb.GetAllAsync<Web.Models.Offline.OfflineCredentialDto>(OfflineStores.Credential);
            var cred = creds.FirstOrDefault();
            if (cred is not null && cred.AssignedStoreIds.Count > 0)
            {
                var fallbackId = cred.AssignedStoreIds[0];
                var stores = await _idb.GetAllAsync<Web.Models.Offline.OfflineStoreDto>(OfflineStores.Stores);
                var match = stores.FirstOrDefault(s => s.StoreId == fallbackId);
                return (fallbackId, match?.NameEn, match?.NameAr);
            }
        }
        catch { }

        return (null, null, null);
    }

    private static ShiftDto MapToShiftDto(OfflineShiftDto src) => new()
    {
        Id = src.ShiftId,
        CashierId = src.CashierId,
        WarehouseId = src.StoreId,
        WarehouseNameEn = src.StoreNameEn,
        WarehouseNameAr = src.StoreNameAr,
        StartTime = src.StartTime,
        EndTime = src.EndTime,
        CashIn = src.CashIn,
        CashOut = src.CashOut,
        TotalSales = src.TotalSales,
        TotalReturns = src.TotalReturns,
        Status = ParseEnum(src.Status, ShiftStatus.Active),
        Comments = src.Comments,
        CreatedAt = src.StartTime
    };

    private static OfflineShiftDto MapServerShiftToOffline(ShiftDto src) => new()
    {
        ShiftId = src.Id,
        CashierId = src.CashierId,
        StoreId = src.WarehouseId,
        StoreNameEn = src.WarehouseNameEn,
        StoreNameAr = src.WarehouseNameAr,
        StartTime = src.StartTime,
        EndTime = src.EndTime,
        CashIn = src.CashIn,
        CashOut = src.CashOut,
        TotalSales = src.TotalSales,
        TotalReturns = src.TotalReturns,
        Status = src.Status.ToString(),
        Comments = src.Comments
    };

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, true, out var v) ? v : fallback;
}
