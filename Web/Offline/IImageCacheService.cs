namespace Web.Offline;

public class ImageSyncSummary
{
    public int Synced { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Removed { get; set; }
}

public class ImageCacheEntry
{
    public string Url { get; set; } = string.Empty;
    public string? Etag { get; set; }
}

// Typed C# wrapper around the `CashierImageCache` JS module. Backs onto the
// browser Cache API so thumbnails survive page reloads with no IndexedDB cost.
public interface IImageCacheService
{
    Task<ImageSyncSummary> SyncImagesAsync(IEnumerable<ImageCacheEntry> entries);
    Task<string?> GetLocalUrlAsync(string url);
    Task<int> PruneStaleAsync(IEnumerable<string> activeUrls);
}
