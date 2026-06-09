using Microsoft.JSInterop;

namespace Web.Offline;

// Concrete IIndexedDbService that bridges into `window.CashierIdb`. Errors from
// the JS side bubble up as JSException — callers should treat them as transient
// (e.g. IDB unavailable in private browsing) and fall back to the online path.
public class IndexedDbService : IIndexedDbService
{
    private readonly IJSRuntime _js;

    public IndexedDbService(IJSRuntime js)
    {
        _js = js;
    }

    public Task UpsertAsync<T>(string storeName, T value)
        => _js.InvokeVoidAsync("CashierIdb.upsert", storeName, value).AsTask();

    public async Task<int> BulkUpsertAsync<T>(string storeName, IEnumerable<T> values)
    {
        var array = values as T[] ?? values.ToArray();
        if (array.Length == 0) return 0;
        return await _js.InvokeAsync<int>("CashierIdb.bulkUpsert", storeName, array);
    }

    public async Task<T?> GetByKeyAsync<T>(string storeName, object key)
    {
        return await _js.InvokeAsync<T?>("CashierIdb.getByKey", storeName, key);
    }

    public async Task<List<T>> GetAllAsync<T>(string storeName)
    {
        var result = await _js.InvokeAsync<List<T>?>("CashierIdb.getAll", storeName);
        return result ?? new List<T>();
    }

    public async Task<List<T>> GetByIndexAsync<T>(string storeName, string indexName, object key)
    {
        var result = await _js.InvokeAsync<List<T>?>("CashierIdb.getByIndex", storeName, indexName, key);
        return result ?? new List<T>();
    }

    public Task DeleteByKeyAsync(string storeName, object key)
        => _js.InvokeVoidAsync("CashierIdb.deleteByKey", storeName, key).AsTask();

    public Task ClearAsync(string storeName)
        => _js.InvokeVoidAsync("CashierIdb.clear", storeName).AsTask();

    public async Task<int> ReplaceAllAsync<T>(string storeName, IEnumerable<T> values)
    {
        var array = values as T[] ?? values.ToArray();
        return await _js.InvokeAsync<int>("CashierIdb.replaceAll", storeName, array);
    }

    public Task<long> AppendAsync<T>(string storeName, T value)
        => _js.InvokeAsync<long>("CashierIdb.append", storeName, value).AsTask();

    public Task<int> CountAsync(string storeName)
        => _js.InvokeAsync<int>("CashierIdb.count", storeName).AsTask();
}
