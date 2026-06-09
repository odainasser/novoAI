using System.Net.Http.Json;
using System.Text.Json;
using Web.Models.Offline;
using Web.Models.Orders;
using Web.Models.Shifts;

namespace Web.Offline;

// Two responsibilities:
//   1. Pull: hits /api/cashier-offline/data, writes everything into IndexedDB
//      in parallel transactions, then triggers an image sync.
//   2. Flush: walks sync_queue in order, sends each item to its endpoint,
//      deletes successful ones, retries failures next time.
public class OfflineSyncService : IOfflineSyncService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IIndexedDbService _idb;
    private readonly IImageCacheService _imageCache;

    public OfflineSyncService(HttpClient http, IIndexedDbService idb, IImageCacheService imageCache)
    {
        _http = http;
        _idb = idb;
        _imageCache = imageCache;
    }

    public async Task<OfflineSyncResult> PullAllAsync(CancellationToken cancellationToken = default)
    {
        CashierOfflineDataResponse? payload;
        try
        {
            payload = await _http.GetFromJsonAsync<CashierOfflineDataResponse>(
                "api/cashier-offline/data", cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OfflineSync] pull failed: {ex.Message}");
            return new OfflineSyncResult { Success = false, Message = ex.Message };
        }

        if (payload is null)
        {
            Console.WriteLine("[OfflineSync] server returned an empty payload");
            return new OfflineSyncResult { Success = false, Message = "Empty offline payload" };
        }

        var distinctProductStores = payload.Products
            .Select(p => p.StoreId)
            .Distinct()
            .Count();
        Console.WriteLine(
            $"[OfflineSync] payload received: stores={payload.Stores.Count}, products={payload.Products.Count} " +
            $"(across {distinctProductStores} stores), shifts={payload.Shifts.Count}, orders={payload.Orders.Count}");

        // Preserve local placeholders for anything still in the sync queue —
        // wiping them during a pull is what causes "I went online and lost my
        // offline shift / order" reports. TargetId on the queue item points to
        // the local-only entity ID.
        HashSet<Guid> pendingIds;
        try
        {
            var pending = await _idb.GetAllAsync<SyncQueueItem>(OfflineStores.SyncQueue);
            pendingIds = pending
                .Where(i => i.TargetId.HasValue)
                .Select(i => i.TargetId!.Value)
                .ToHashSet();
        }
        catch
        {
            pendingIds = new HashSet<Guid>();
        }

        List<OfflineShiftDto> preservedShifts;
        List<OfflineOrderDto> preservedOrders;
        try
        {
            preservedShifts = (await _idb.GetAllAsync<OfflineShiftDto>(OfflineStores.Shifts))
                .Where(s => pendingIds.Contains(s.ShiftId)).ToList();
            preservedOrders = (await _idb.GetAllAsync<OfflineOrderDto>(OfflineStores.Orders))
                .Where(o => pendingIds.Contains(o.OrderId)).ToList();
        }
        catch
        {
            preservedShifts = new List<OfflineShiftDto>();
            preservedOrders = new List<OfflineOrderDto>();
        }

        // Atomic replaces so an unexpected payload (or a write failure midway)
        // can never leave the user with an empty cache. ReplaceAllAsync wraps
        // both the clear and the puts in a single IDB transaction, and we skip
        // the replace entirely when the server returns an empty list but the
        // cache has data — that almost always means a transient backend issue
        // rather than an actual catalog wipe.
        try
        {
            await WriteCredentialAsync(payload.Credential);
            await _idb.UpsertAsync(OfflineStores.Profile, payload.Profile);

            await ReplaceWithGuardAsync(OfflineStores.Stores, payload.Stores);

            // Replay every pending CreateOrder / PartialRefund against the fresh
            // product list before we write it to IDB — otherwise an unflushed
            // offline sale would silently get its stock decrement undone when
            // the pull replaces the cache with server-authoritative numbers.
            await ApplyPendingStockDeltasAsync(payload.Products, payload.Orders, preservedOrders);
            await ReplaceWithGuardAsync(OfflineStores.Products, payload.Products);

            // Shifts / orders combine fresh server data with any local rows
            // still referenced by the sync queue (offline-only items that
            // haven't been replayed yet).
            var shiftBatch = payload.Shifts.Concat(preservedShifts).ToList();
            var orderBatch = payload.Orders.Concat(preservedOrders).ToList();
            await ReplaceWithGuardAsync(OfflineStores.Shifts, shiftBatch);
            await ReplaceWithGuardAsync(OfflineStores.Orders, orderBatch);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OfflineSync] IndexedDB write failed: {ex.Message}");
            return new OfflineSyncResult { Success = false, Message = $"IndexedDB write failed: {ex.Message}" };
        }

        Console.WriteLine("[OfflineSync] cache write complete");

        var entries = payload.Products
            .Where(p => !string.IsNullOrWhiteSpace(p.ThumbnailUrl) || !string.IsNullOrWhiteSpace(p.ImageUrl))
            .Select(p => new ImageCacheEntry
            {
                Url = p.ThumbnailUrl ?? p.ImageUrl!,
                Etag = p.ImageETag
            })
            .ToList();

        ImageSyncSummary? imageSummary = null;
        try
        {
            imageSummary = await _imageCache.SyncImagesAsync(entries);
        }
        catch
        {
            // Image sync failures shouldn't fail the whole pull — the panel
            // can still function with missing thumbnails.
        }

        var itemsPulled = payload.Stores.Count + payload.Products.Count + payload.Shifts.Count + payload.Orders.Count;
        return new OfflineSyncResult
        {
            Success = true,
            ItemsPulled = itemsPulled,
            Images = imageSummary
        };
    }

    public async Task<OfflineFlushResult> FlushQueueAsync(CancellationToken cancellationToken = default)
    {
        var items = await _idb.GetAllAsync<SyncQueueItem>(OfflineStores.SyncQueue);
        var ordered = items.Where(i => i.Seq.HasValue).OrderBy(i => i.Seq).ToList();

        Console.WriteLine($"[OfflineSync] flush starting: {ordered.Count} pending");

        int flushed = 0, failed = 0;

        foreach (var item in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DispatchOutcome outcome;
            string? thrown = null;

            try
            {
                outcome = await DispatchAsync(item, cancellationToken);
            }
            catch (Exception ex)
            {
                outcome = DispatchOutcome.TransientFailure;
                thrown = ex.Message;
            }

            if (outcome == DispatchOutcome.Success)
            {
                try
                {
                    await _idb.DeleteByKeyAsync(OfflineStores.SyncQueue, item.Seq!.Value);
                    flushed++;
                    Console.WriteLine($"[OfflineSync] flushed #{item.Seq} {item.Op}");
                }
                catch (Exception ex)
                {
                    // If we can't delete after a successful post we'll re-try on
                    // the next flush. Server endpoints are idempotent on these
                    // legacy/duplicate replays.
                    failed++;
                    Console.WriteLine($"[OfflineSync] post-flush delete failed for #{item.Seq}: {ex.Message}");
                }
                continue;
            }

            // Both failure paths: persist LastError so the panel shows what's wrong.
            item.Attempts++;
            if (thrown is not null) item.LastError = thrown;
            try { await _idb.UpsertAsync(OfflineStores.SyncQueue, item); } catch { }

            if (outcome == DispatchOutcome.PermanentFailure)
            {
                // Drop the broken entry so it stops blocking the rest of the
                // queue — replaying it will never succeed.
                Console.WriteLine($"[OfflineSync] dropping permanently failed #{item.Seq} {item.Op}: {item.LastError}");
                try { await _idb.DeleteByKeyAsync(OfflineStores.SyncQueue, item.Seq!.Value); } catch { }
                failed++;
                continue;
            }

            // Transient: keep ordering — stop here, retry next flush.
            failed++;
            Console.WriteLine($"[OfflineSync] transient failure on #{item.Seq} {item.Op}: {item.LastError}");
            break;
        }

        if (flushed > 0)
        {
            // Refresh local caches so order numbers / shift totals reflect what
            // the server just persisted.
            try { await PullAllAsync(cancellationToken); } catch { }
        }

        var remaining = await _idb.CountAsync(OfflineStores.SyncQueue);
        Console.WriteLine($"[OfflineSync] flush complete: flushed={flushed} failed={failed} remaining={remaining}");
        return new OfflineFlushResult { Flushed = flushed, Failed = failed, Remaining = remaining };
    }

    public async Task<long> EnqueueAsync(SyncQueueItem item)
    {
        item.Seq = null; // let IDB assign
        item.QueuedAtUtc = DateTime.UtcNow;
        return await _idb.AppendAsync(OfflineStores.SyncQueue, item);
    }

    public Task<int> CountPendingAsync() => _idb.CountAsync(OfflineStores.SyncQueue);

    // --- internals -------------------------------------------------------

    // Atomic store replacement with an "empty payload" guard. If the server
    // payload for a domain is empty but the cache already holds rows, we keep
    // the existing data rather than wipe it — that scenario almost always
    // means a transient backend issue (or a cashier in transition between
    // store assignments) rather than an actual catalog removal, and silently
    // emptying the cache is exactly what loses the cashier's local data on
    // refresh.
    private async Task ReplaceWithGuardAsync<T>(string storeName, IReadOnlyList<T> incoming)
    {
        if (incoming.Count == 0)
        {
            int existing;
            try { existing = await _idb.CountAsync(storeName); }
            catch { existing = 0; }

            if (existing > 0)
            {
                Console.WriteLine($"[OfflineSync] skipping wipe of '{storeName}' — server returned empty but cache has {existing} rows");
                return;
            }
        }

        await _idb.ReplaceAllAsync(storeName, incoming);
    }

    private Task WriteCredentialAsync(OfflineCredentialDto incoming)
        => _idb.UpsertAsync(OfflineStores.Credential, incoming);

    // Walks the sync queue and replays each pending mutation against the fresh
    // products payload before it overwrites the cache. CreateOrder lines
    // subtract `qty × unit.Quantity` base units; PartialRefund lines add the
    // same back, resolved through OrderItemId → unit/product on freshly-pulled
    // or locally-preserved orders. Without this, an offline sale that hasn't
    // replayed yet would have its stock decrement quietly erased by the pull,
    // and the low-stock / out-of-stock indicators the cashier just saw would
    // bounce back to pre-sale values.
    private async Task ApplyPendingStockDeltasAsync(
        List<OfflineProductDto> freshProducts,
        List<OfflineOrderDto> serverOrders,
        List<OfflineOrderDto> preservedOrders)
    {
        if (freshProducts.Count == 0) return;

        List<SyncQueueItem> queue;
        try
        {
            queue = (await _idb.GetAllAsync<SyncQueueItem>(OfflineStores.SyncQueue))
                .Where(i => i.Seq.HasValue)
                .OrderBy(i => i.Seq)
                .ToList();
        }
        catch
        {
            return;
        }
        if (queue.Count == 0) return;

        // Compound key: (storeId, productId, unitId) — products are scoped per
        // store, and each product may have multiple selling units.
        var unitsByStoreProductUnit = freshProducts.SelectMany(p => p.Units.Select(u => new
        {
            p.StoreId,
            p.ProductId,
            Unit = u
        })).ToDictionary(x => (x.StoreId, x.ProductId, x.Unit.UnitId), x => x.Unit);
        var productsByStoreProduct = freshProducts.ToDictionary(p => (p.StoreId, p.ProductId));

        // Order lookup for refunds — search both the fresh server orders and
        // the locally-preserved (offline-only) orders.
        var ordersById = new Dictionary<Guid, OfflineOrderDto>();
        foreach (var o in serverOrders) ordersById[o.OrderId] = o;
        foreach (var o in preservedOrders) ordersById[o.OrderId] = o;

        foreach (var item in queue)
        {
            try
            {
                switch (item.Op)
                {
                    case SyncQueueOpType.CreateOrder:
                    {
                        var queued = JsonSerializer.Deserialize<QueuedCreateOrder>(item.PayloadJson, _json);
                        if (queued is null) continue;
                        var storeId = queued.StoreId ?? item.StoreId ?? Guid.Empty;
                        foreach (var line in queued.Request.Items)
                        {
                            if (!unitsByStoreProductUnit.TryGetValue((storeId, line.ProductId, line.UnitId), out var unit))
                                continue;
                            var baseDelta = line.Quantity * Math.Max(unit.Quantity, 1);
                            unit.AvailableQuantity = Math.Max(0, unit.AvailableQuantity - baseDelta);

                            if (productsByStoreProduct.TryGetValue((storeId, line.ProductId), out var product))
                                product.AvailableQuantity = Math.Max(0, product.AvailableQuantity - baseDelta);
                        }
                        break;
                    }

                    case SyncQueueOpType.PartialRefund:
                    {
                        var queued = JsonSerializer.Deserialize<QueuedPartialRefund>(item.PayloadJson, _json);
                        if (queued is null) continue;
                        if (!ordersById.TryGetValue(queued.OrderId, out var order)) continue;
                        var storeId = order.StoreId ?? item.StoreId ?? Guid.Empty;
                        foreach (var refundLine in queued.Items)
                        {
                            var orderItem = order.Items.FirstOrDefault(i => i.OrderItemId == refundLine.OrderItemId);
                            if (orderItem is null || !orderItem.UnitId.HasValue) continue;
                            if (!unitsByStoreProductUnit.TryGetValue((storeId, orderItem.ProductId, orderItem.UnitId.Value), out var unit))
                                continue;
                            var baseDelta = refundLine.Quantity * Math.Max(unit.Quantity, 1);
                            unit.AvailableQuantity += baseDelta;

                            if (productsByStoreProduct.TryGetValue((storeId, orderItem.ProductId), out var product))
                                product.AvailableQuantity += baseDelta;
                        }
                        break;
                    }

                    // StartShift / EndShift don't touch stock — skip.
                    default:
                        continue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OfflineSync] failed to replay stock delta for #{item.Seq} {item.Op}: {ex.Message}");
            }
        }
    }

    // Dispatch outcomes:
    //   Success          — server accepted (or "already in target state"); delete from queue.
    //   PermanentFailure — request is broken and replaying it will never succeed; delete
    //                      from queue so it stops blocking the rest, log the reason.
    //   TransientFailure — likely network/auth/server hiccup; keep in queue and stop the
    //                      flush so we don't reorder later items.
    private enum DispatchOutcome { Success, PermanentFailure, TransientFailure }

    private async Task<DispatchOutcome> DispatchAsync(SyncQueueItem item, CancellationToken cancellationToken)
    {
        HttpResponseMessage? resp = null;
        switch (item.Op)
        {
            case SyncQueueOpType.CreateOrder:
            {
                var queued = JsonSerializer.Deserialize<QueuedCreateOrder>(item.PayloadJson, _json);
                if (queued is null) return DispatchOutcome.PermanentFailure;
                resp = await _http.PostAsJsonAsync("api/orders", queued.Request, _json, cancellationToken);
                break;
            }

            case SyncQueueOpType.PartialRefund:
            {
                var queued = JsonSerializer.Deserialize<QueuedPartialRefund>(item.PayloadJson, _json);
                if (queued is null || queued.OrderId == Guid.Empty) return DispatchOutcome.PermanentFailure;
                resp = await _http.PostAsJsonAsync(
                    $"api/orders/{queued.OrderId}/partial-refund",
                    new { Items = queued.Items, ClientCreatedAt = queued.ClientCreatedAt },
                    _json,
                    cancellationToken);
                break;
            }

            case SyncQueueOpType.StartShift:
            {
                var queued = JsonSerializer.Deserialize<QueuedStartShift>(item.PayloadJson, _json);
                if (queued is null) return DispatchOutcome.PermanentFailure;
                resp = await _http.PostAsJsonAsync("api/shifts/start", queued.Request, _json, cancellationToken);
                break;
            }

            case SyncQueueOpType.EndShift:
            {
                var queued = JsonSerializer.Deserialize<QueuedEndShift>(item.PayloadJson, _json);
                if (queued is null || queued.ShiftId == Guid.Empty) return DispatchOutcome.PermanentFailure;
                resp = await _http.PostAsJsonAsync(
                    $"api/shifts/{queued.ShiftId}/end",
                    queued.Request,
                    _json,
                    cancellationToken);
                break;
            }

            default:
                return DispatchOutcome.PermanentFailure;
        }

        if (resp.IsSuccessStatusCode) return DispatchOutcome.Success;

        string body = string.Empty;
        try { body = await resp.Content.ReadAsStringAsync(cancellationToken); } catch { }
        var lower = body?.ToLowerInvariant() ?? string.Empty;
        var status = (int)resp.StatusCode;

        // Idempotent "already in desired state" responses — treat as success so
        // a duplicate queue entry (e.g. legacy queue item from before the fix)
        // doesn't permanently block the rest of the queue.
        if (item.Op == SyncQueueOpType.StartShift && status == 400 && lower.Contains("active shift already exists"))
            return DispatchOutcome.Success;
        if (item.Op == SyncQueueOpType.EndShift && (status == 404 || (status == 400 && lower.Contains("already completed"))))
            return DispatchOutcome.Success;

        item.LastError = $"HTTP {status} {resp.ReasonPhrase}: {body}".Trim();

        // 401 (auth expired) and 408/429/5xx (transient) — keep the item and
        // halt so we don't lose ordering for the remaining writes.
        if (status == 401 || status == 408 || status == 429 || status >= 500)
            return DispatchOutcome.TransientFailure;

        // Anything else (400, 403, 404 on a non-shift op, etc.) is a permanent
        // failure for this item. Drop it so subsequent items get a chance.
        return DispatchOutcome.PermanentFailure;
    }
}
