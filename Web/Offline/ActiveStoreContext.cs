using Blazored.LocalStorage;

namespace Web.Offline;

// Active store for the cashier panel. The selection is persisted to
// localStorage so a hard reload (especially offline, where the server can't
// be consulted) restores the cashier's switched store instead of falling
// back to the first assigned one. Listeners can subscribe to OnChanged to
// refresh UI when the store is switched.
public class ActiveStoreContext
{
    private const string StorageKey = "cashier.activeStore";

    private readonly ILocalStorageService _localStorage;
    private readonly object _lock = new();
    private Task? _loadTask;

    public Guid? StoreId { get; private set; }
    public string? StoreNameEn { get; private set; }
    public string? StoreNameAr { get; private set; }

    public event Action? OnChanged;

    public ActiveStoreContext(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    // Single-flight rehydrate from localStorage. The first caller after app
    // start performs the read; all later callers await the same Task. Offline
    // wrappers call this before reading StoreId so a hard reload doesn't see
    // null and silently fall back to the first assigned store.
    public Task EnsureLoadedAsync()
    {
        lock (_lock)
        {
            _loadTask ??= LoadFromStorageAsync();
            return _loadTask;
        }
    }

    private async Task LoadFromStorageAsync()
    {
        try
        {
            var saved = await _localStorage.GetItemAsync<PersistedActiveStore?>(StorageKey);
            if (saved is null || saved.StoreId == Guid.Empty)
                return;

            bool changed;
            lock (_lock)
            {
                // If something already called Set() before the load finished,
                // honour the newer value rather than overwriting it.
                if (StoreId.HasValue)
                {
                    changed = false;
                }
                else
                {
                    StoreId = saved.StoreId;
                    StoreNameEn = saved.NameEn;
                    StoreNameAr = saved.NameAr;
                    changed = true;
                }
            }

            if (changed) OnChanged?.Invoke();
        }
        catch
        {
        }
    }

    public void Set(Guid storeId, string? nameEn, string? nameAr)
    {
        lock (_lock)
        {
            StoreId = storeId;
            StoreNameEn = nameEn;
            StoreNameAr = nameAr;
            // Mark as loaded so a later EnsureLoadedAsync doesn't race-overwrite.
            _loadTask ??= Task.CompletedTask;
        }
        OnChanged?.Invoke();
        _ = PersistAsync(new PersistedActiveStore { StoreId = storeId, NameEn = nameEn, NameAr = nameAr });
    }

    public void Clear()
    {
        lock (_lock)
        {
            StoreId = null;
            StoreNameEn = null;
            StoreNameAr = null;
            _loadTask = Task.CompletedTask;
        }
        OnChanged?.Invoke();
        _ = ClearStorageAsync();
    }

    public bool IsSelected => StoreId.HasValue;

    private async Task PersistAsync(PersistedActiveStore data)
    {
        try { await _localStorage.SetItemAsync(StorageKey, data); }
        catch { }
    }

    private async Task ClearStorageAsync()
    {
        try { await _localStorage.RemoveItemAsync(StorageKey); }
        catch { }
    }

    private class PersistedActiveStore
    {
        public Guid StoreId { get; set; }
        public string? NameEn { get; set; }
        public string? NameAr { get; set; }
    }
}
