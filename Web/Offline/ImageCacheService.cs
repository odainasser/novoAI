using Microsoft.JSInterop;

namespace Web.Offline;

public class ImageCacheService : IImageCacheService
{
    private readonly IJSRuntime _js;

    public ImageCacheService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<ImageSyncSummary> SyncImagesAsync(IEnumerable<ImageCacheEntry> entries)
    {
        var payload = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Url))
            .Select(e => new { url = e.Url, etag = e.Etag })
            .ToArray();

        if (payload.Length == 0)
            return new ImageSyncSummary();

        return await _js.InvokeAsync<ImageSyncSummary>("CashierImageCache.syncImages", new object[] { payload });
    }

    public async Task<string?> GetLocalUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return await _js.InvokeAsync<string?>("CashierImageCache.getLocalUrl", url);
    }

    public async Task<int> PruneStaleAsync(IEnumerable<string> activeUrls)
    {
        var array = activeUrls.Where(u => !string.IsNullOrWhiteSpace(u)).ToArray();
        return await _js.InvokeAsync<int>("CashierImageCache.pruneStale", new object[] { array });
    }
}
