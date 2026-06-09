namespace Web.Offline;

// Typed C# wrapper around the `CashierIdb` JS module exposed by cashier-offline.js.
// Members map 1:1 to the JS API, so reasoning about transactional semantics happens
// in one place. All methods are awaitable.
public interface IIndexedDbService
{
    Task UpsertAsync<T>(string storeName, T value);
    Task<int> BulkUpsertAsync<T>(string storeName, IEnumerable<T> values);
    Task<T?> GetByKeyAsync<T>(string storeName, object key);
    Task<List<T>> GetAllAsync<T>(string storeName);
    Task<List<T>> GetByIndexAsync<T>(string storeName, string indexName, object key);
    Task DeleteByKeyAsync(string storeName, object key);
    Task ClearAsync(string storeName);
    // Atomic clear+write in a single transaction. The store is either fully
    // replaced or left untouched — there's no in-between state where a read
    // could see an empty cache.
    Task<int> ReplaceAllAsync<T>(string storeName, IEnumerable<T> values);
    // Append using the auto-increment key — returns the assigned seq.
    Task<long> AppendAsync<T>(string storeName, T value);
    Task<int> CountAsync(string storeName);
}
