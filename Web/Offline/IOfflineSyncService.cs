using Web.Models.Offline;

namespace Web.Offline;

public class OfflineSyncResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int ItemsPulled { get; set; }
    public ImageSyncSummary? Images { get; set; }
}

public class OfflineFlushResult
{
    public int Flushed { get; set; }
    public int Failed { get; set; }
    public int Remaining { get; set; }
}

public interface IOfflineSyncService
{
    // Pull-down: fetch full offline payload and write everything into IndexedDB.
    Task<OfflineSyncResult> PullAllAsync(CancellationToken cancellationToken = default);

    // Push: walk the sync queue in order, POST each item to its endpoint, delete on success.
    Task<OfflineFlushResult> FlushQueueAsync(CancellationToken cancellationToken = default);

    // Queue helpers used by the offline-aware wrapper services.
    Task<long> EnqueueAsync(SyncQueueItem item);
    Task<int> CountPendingAsync();
}
