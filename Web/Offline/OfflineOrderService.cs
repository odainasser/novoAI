using System.Text.Json;
using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Offline;
using Web.Models.Orders;
using Web.Services;

namespace Web.Offline;

// Offline-aware decorator around IOrderService.
//
//   Reads (GetMyOrders, GetOrderById, statistics): try the live service first;
//   on transient failure (server down behind 'navigator.onLine = true') fall
//   back to the IndexedDB cache scoped to the active store.
//
//   Writes (CreateOrder, PartialRefund): always persist to IndexedDB *and*
//   append a sync_queue record. If we can reach the server the queue stays
//   clean and the live result wins; otherwise the queue entry survives and
//   the next flush replays it. The local mutation simulates the server's
//   math (prices, VAT, stock decrement, status) so the panel reads the same
//   values whether the device is online or offline.
//
//   Refunds offline are only honoured against orders we have cached locally.
public class OfflineOrderService : IOrderService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // Mirror of Infrastructure.Services.OrderService.VatRate. Keep in lockstep
    // — any divergence shows up as a 1–2 fil rounding mismatch between the
    // offline receipt and the post-sync server receipt.
    private const decimal VatRate = 0.05m;

    private readonly IOrderService _inner;
    private readonly IIndexedDbService _idb;
    private readonly ActiveStoreContext _activeStore;
    private readonly OfflineNetworkMonitor _network;
    private readonly IOfflineSyncService _sync;

    public OfflineOrderService(
        IOrderService inner,
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

    public Task<PaginatedList<OrderDto>> GetAllOrdersAsync(
        int pageNumber, int pageSize, string? search = null, OrderStatus? status = null,
        OrderChannel? channel = null, PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null, DateTime? toDate = null,
        Guid? warehouseId = null, Guid? cashierId = null,
        Guid? branchId = null)
        => _inner.GetAllOrdersAsync(pageNumber, pageSize, search, status, channel, paymentMethod, fromDate, toDate, warehouseId, cashierId, branchId);

    public Task<PaginatedList<OrderDto>> GetOrdersByCashierAsync(
        Guid cashierId, int pageNumber, int pageSize, string? search = null,
        OrderStatus? status = null, PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null, DateTime? toDate = null)
        => _inner.GetOrdersByCashierAsync(cashierId, pageNumber, pageSize, search, status, paymentMethod, fromDate, toDate);

    public async Task<PaginatedList<OrderDto>> GetMyOrdersAsync(
        int pageNumber, int pageSize, string? search = null, OrderStatus? status = null,
        PaymentMethod? paymentMethod = null, DateTime? fromDate = null,
        DateTime? toDate = null, Guid? warehouseId = null)
    {
        await _activeStore.EnsureLoadedAsync();

        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.GetMyOrdersAsync(pageNumber, pageSize, search, status, paymentMethod, fromDate, toDate, warehouseId); }
            catch { /* fall through to cache */ }
        }

        var scopedStoreId = _activeStore.StoreId ?? warehouseId;

        try
        {
            var cached = scopedStoreId.HasValue
                ? await _idb.GetByIndexAsync<OfflineOrderDto>(
                    OfflineStores.Orders, OfflineStores.Indexes.OrdersByStore, scopedStoreId.Value)
                : await _idb.GetAllAsync<OfflineOrderDto>(OfflineStores.Orders);

            var mapped = cached
                .OrderByDescending(o => o.CreatedAt)
                .Select(MapToOrderDto)
                .ToList();
            var filtered = ApplyMyFilters(mapped, search, status, paymentMethod, fromDate, toDate);
            var total = filtered.Count;
            var page = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return new PaginatedList<OrderDto>(page, total, pageNumber, pageSize);
        }
        catch
        {
            return new PaginatedList<OrderDto>(new List<OrderDto>(), 0, pageNumber, pageSize);
        }
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid id)
    {
        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.GetOrderByIdAsync(id); }
            catch { /* fall through to cache */ }
        }

        try
        {
            var cached = await _idb.GetByKeyAsync<OfflineOrderDto>(OfflineStores.Orders, id);
            return cached is null ? null : MapToOrderDto(cached);
        }
        catch
        {
            return null;
        }
    }

    public Task<OrderDto?> GetOrderByNumberAsync(string orderNumber) => _inner.GetOrderByNumberAsync(orderNumber);

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        await _activeStore.EnsureLoadedAsync();

        // Stable idempotency key for the whole lifetime of this sale — set BEFORE
        // the online attempt so that if the server creates the order but the
        // response is lost in transit, the later offline replay carries the same
        // key and the server de-duplicates instead of creating a second order.
        request.IdempotencyKey ??= Guid.NewGuid().ToString();

        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.CreateOrderAsync(request);
                if (live is not null)
                {
                    try { await _idb.UpsertAsync(OfflineStores.Orders, MapServerOrderToOffline(live, _activeStore.StoreId)); } catch { }
                    return live;
                }
            }
            catch { /* fall through to offline path so the sale isn't lost */ }
        }

        // Offline (or live call failed): persist a local placeholder + queue
        // the original request so the next flush replays it server-side.
        // Stamp ClientCreatedAt so the server records the actual sale time on replay
        // instead of the sync moment. Use the same instant for cache + queue so both
        // views agree before and after sync.
        var saleTime = DateTime.UtcNow;
        request.ClientCreatedAt = saleTime;

        var localId = Guid.NewGuid();
        // IdempotencyKey was already stamped at the top of this method so the
        // queued replay de-duplicates against any order the server may have
        // created during a failed/lost online attempt.
        var resolvedStoreId = await ResolveStoreIdAsync();
        var localOrder = await BuildPricedLocalOrderAsync(localId, request, saleTime, resolvedStoreId);

        try
        {
            // Decrement cached stock on each touched product so the POS sees
            // the post-sale availability immediately. Server replay applies
            // the same delta server-side.
            await ApplyLocalStockDecrementsAsync(request, resolvedStoreId);

            await _idb.UpsertAsync(OfflineStores.Orders, localOrder);
            await _sync.EnqueueAsync(new SyncQueueItem
            {
                Op = SyncQueueOpType.CreateOrder,
                StoreId = resolvedStoreId,
                TargetId = localId,
                PayloadJson = JsonSerializer.Serialize(new QueuedCreateOrder
                {
                    LocalOrderId = localId,
                    StoreId = resolvedStoreId,
                    Request = request
                }, _json)
            });
        }
        catch { }

        return MapToOrderDto(localOrder);
    }

    public Task<OrderDto> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request) => _inner.UpdateOrderStatusAsync(id, request);
    public Task<OrderDto?> CancelOrderAsync(Guid id, string? reason) => _inner.CancelOrderAsync(id, reason);

    public async Task<OrderDto?> PartialRefundAsync(Guid id, List<PartialRefundItemRequest> items)
    {
        var cached = await TryGetCachedOrderAsync(id);

        if (await _network.IsOnlineAsync())
        {
            try
            {
                var live = await _inner.PartialRefundAsync(id, items);
                if (live is not null)
                {
                    try { await _idb.UpsertAsync(OfflineStores.Orders, MapServerOrderToOffline(live, cached?.StoreId)); } catch { }
                    return live;
                }
            }
            catch { /* fall through to offline simulation */ }
        }

        if (cached is null)
            throw new InvalidOperationException("Refunds are only available for cached orders while offline.");

        var refundTime = DateTime.UtcNow;

        await _sync.EnqueueAsync(new SyncQueueItem
        {
            Op = SyncQueueOpType.PartialRefund,
            StoreId = cached.StoreId,
            TargetId = id,
            PayloadJson = JsonSerializer.Serialize(new QueuedPartialRefund { OrderId = id, Items = items, ClientCreatedAt = refundTime }, _json)
        });

        try
        {
            await SimulateOfflineRefundAsync(cached, items, refundTime);
            await _idb.UpsertAsync(OfflineStores.Orders, cached);
        }
        catch { }

        return MapToOrderDto(cached);
    }

    public Task<OrderDto?> PartialRefundAsync(Guid id, decimal amount) => _inner.PartialRefundAsync(id, amount);

    public async Task<OrderStatisticsDto> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.GetOrderStatisticsAsync(fromDate, toDate); }
            catch { /* fall through to cache */ }
        }
        return await ComputeStatisticsFromCacheAsync(fromDate, toDate);
    }

    public async Task<OrderStatisticsDto> GetMyStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.GetMyStatisticsAsync(fromDate, toDate); }
            catch { /* fall through to cache */ }
        }
        return await ComputeStatisticsFromCacheAsync(fromDate, toDate);
    }

    public Task<byte[]> ExportOrdersToExcelAsync(
        string? search = null, OrderStatus? status = null, OrderChannel? channel = null,
        PaymentMethod? paymentMethod = null, DateTime? fromDate = null, DateTime? toDate = null,
        Guid? warehouseId = null, Guid? cashierId = null, bool isArabic = false, Guid? branchId = null)
        => _inner.ExportOrdersToExcelAsync(search, status, channel, paymentMethod, fromDate, toDate, warehouseId, cashierId, isArabic, branchId);

    // ---------------- helpers ----------------

    private async Task<OfflineOrderDto?> TryGetCachedOrderAsync(Guid orderId)
    {
        try { return await _idb.GetByKeyAsync<OfflineOrderDto>(OfflineStores.Orders, orderId); }
        catch { return null; }
    }

    // Builds a fully-priced local order from the cached product catalog. Mirrors
    // Infrastructure.Services.OrderService.CreateOrder so the cashier sees the
    // same totals offline that the server will record on replay.
    private async Task<OfflineOrderDto> BuildPricedLocalOrderAsync(Guid localId, CreateOrderRequest request, DateTime saleTime, Guid? storeId)
    {
        var products = await TryReadProductsForStoreAsync(storeId);
        // Use a tolerant grouping rather than ToDictionary — if storeId still
        // ends up null (no active store + no credential), the cache holds the
        // same productId once per assigned store. We prefer the row whose
        // StoreId matches the resolved storeId, falling back to the first.
        var productsById = products
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g =>
                storeId.HasValue
                    ? (g.FirstOrDefault(p => p.StoreId == storeId.Value) ?? g.First())
                    : g.First());

        var items = new List<OfflineOrderItemDto>(request.Items.Count);
        decimal subtotal = 0m;

        foreach (var line in request.Items)
        {
            productsById.TryGetValue(line.ProductId, out var product);
            var unit = product?.Units.FirstOrDefault(u => u.UnitId == line.UnitId);

            var unitPrice = unit?.SellingPrice ?? 0m;
            var lineTotal = unitPrice * line.Quantity;
            subtotal += lineTotal;

            items.Add(new OfflineOrderItemDto
            {
                OrderItemId = Guid.NewGuid(),
                ProductId = line.ProductId,
                ProductNameEn = product?.NameEn ?? string.Empty,
                ProductNameAr = product?.NameAr ?? string.Empty,
                ProductCode = product?.Code ?? string.Empty,
                UnitId = line.UnitId,
                UnitNameEn = unit?.UnitNameEn,
                UnitBarcode = unit?.Barcode,
                Quantity = line.Quantity,
                UnitPrice = unitPrice,
                Total = lineTotal
            });
        }

        var vatAmount = Math.Round(subtotal * VatRate, 2);
        var total = subtotal + vatAmount;

        // For Cash / Card payments the server stores null for the other-method
        // amount columns. For Split, the request carries both amounts.
        decimal? cashAmount = request.PaymentMethod switch
        {
            PaymentMethod.Split => request.CashAmount,
            _ => null
        };
        decimal? cardAmount = request.PaymentMethod switch
        {
            PaymentMethod.Split => request.CardAmount,
            _ => null
        };

        var (storeNameEn, storeNameAr) = await ResolveStoreNamesAsync(storeId);
        var (cashierId, cashierName) = await ResolveCashierAsync();
        var activeShiftId = await TryGetActiveShiftIdAsync(storeId);

        return new OfflineOrderDto
        {
            OrderId = localId,
            StoreId = storeId,
            StoreNameEn = storeNameEn,
            StoreNameAr = storeNameAr,
            OrderNumber = $"OFFLINE-{localId:N}".Substring(0, 14),
            Status = OrderStatus.Completed.ToString(),
            PaymentMethod = request.PaymentMethod.ToString(),
            CashAmount = cashAmount,
            CardAmount = cardAmount,
            Subtotal = subtotal,
            VatRate = VatRate,
            VatAmount = vatAmount,
            Total = total,
            CreatedAt = saleTime,
            CashierId = cashierId,
            CashierName = cashierName,
            Items = items
        };
    }

    // Subtracts `Qty × unit.Quantity` (base units) from each touched product/unit
    // in the cached products store. The server applies the same delta on replay
    // and ApplyPendingStockDeltas keeps the deltas alive across pulls.
    private async Task ApplyLocalStockDecrementsAsync(CreateOrderRequest request, Guid? storeId)
    {
        var products = await TryReadProductsForStoreAsync(storeId);
        if (products.Count == 0) return;

        // The cache holds one row per (storeId, productId), so when storeId is
        // null GetAll returns the same productId multiple times. Pick the one
        // matching the resolved storeId, otherwise take the first match — this
        // is the only product we'll mutate so the UI stays consistent with
        // what we wrote during BuildPricedLocalOrderAsync.
        OfflineProductDto? PickRow(Guid productId) =>
            storeId.HasValue
                ? products.FirstOrDefault(p => p.ProductId == productId && p.StoreId == storeId.Value)
                  ?? products.FirstOrDefault(p => p.ProductId == productId)
                : products.FirstOrDefault(p => p.ProductId == productId);

        var touched = new Dictionary<Guid, OfflineProductDto>();
        foreach (var line in request.Items)
        {
            var product = PickRow(line.ProductId);
            if (product is null) continue;
            var unit = product.Units.FirstOrDefault(u => u.UnitId == line.UnitId);
            if (unit is null) continue;

            var baseDelta = line.Quantity * Math.Max(unit.Quantity, 1);
            unit.AvailableQuantity = Math.Max(0, unit.AvailableQuantity - baseDelta);
            product.AvailableQuantity = Math.Max(0, product.AvailableQuantity - baseDelta);
            touched[product.ProductId] = product;
        }

        foreach (var product in touched.Values)
        {
            try { await _idb.UpsertAsync(OfflineStores.Products, product); } catch { }
        }
    }

    // Pulls the most reliable storeId we can find: ActiveStoreContext → cached
    // credential's first assigned store. Without this, offline order writes
    // would silently land with `StoreId = null` and the orders-by-store index
    // wouldn't find them on the next read.
    private async Task<Guid?> ResolveStoreIdAsync()
    {
        if (_activeStore.StoreId.HasValue) return _activeStore.StoreId;
        try
        {
            var creds = await _idb.GetAllAsync<OfflineCredentialDto>(OfflineStores.Credential);
            var cred = creds.FirstOrDefault();
            if (cred is not null && cred.AssignedStoreIds.Count > 0)
                return cred.AssignedStoreIds[0];
        }
        catch { }
        return null;
    }

    // Server-equivalent partial-refund simulation. Mirrors
    // OrderService.PartialRefundAsync — including the per-line VAT in the refund
    // amount, recomputing order totals from remaining items, restoring base-unit
    // stock on touched products, and flipping status to PartialRefunded /
    // Refunded based on whether anything remains.
    private async Task SimulateOfflineRefundAsync(OfflineOrderDto order, List<PartialRefundItemRequest> items, DateTime refundTime)
    {
        var vatRate = order.VatRate > 0m ? order.VatRate : VatRate;
        decimal refundAmount = 0m;

        // refundedQuantities also drives stock restoration below.
        var refundedQuantities = new Dictionary<Guid, int>();

        foreach (var req in items)
        {
            var line = order.Items.FirstOrDefault(i => i.OrderItemId == req.OrderItemId);
            if (line is null) continue;
            var qty = Math.Min(req.Quantity, line.Quantity);
            if (qty <= 0) continue;

            refundedQuantities[line.OrderItemId] = qty;

            var unitVat = line.UnitPrice * vatRate;
            refundAmount += (line.UnitPrice + unitVat) * qty;

            line.Quantity -= qty;
            line.Total = line.UnitPrice * line.Quantity;
        }

        var newSubtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        var newVatAmount = Math.Round(newSubtotal * vatRate, 2);
        order.Subtotal = newSubtotal;
        order.VatAmount = newVatAmount;
        order.Total = newSubtotal + newVatAmount;

        order.Refunds.Add(new OfflineOrderRefundDto
        {
            Id = Guid.NewGuid(),
            Amount = refundAmount,
            CreatedAt = refundTime,
            Reason = "Partial refund"
        });

        order.Status = order.Items.Any(i => i.Quantity > 0)
            ? OrderStatus.PartialRefunded.ToString()
            : OrderStatus.Refunded.ToString();

        await RestoreCachedStockAsync(order, refundedQuantities);
    }

    private async Task RestoreCachedStockAsync(OfflineOrderDto order, Dictionary<Guid, int> refundedQuantities)
    {
        if (refundedQuantities.Count == 0 || !order.StoreId.HasValue) return;
        var products = await TryReadProductsForStoreAsync(order.StoreId);
        if (products.Count == 0) return;

        var touched = new HashSet<Guid>();
        foreach (var line in order.Items)
        {
            if (!refundedQuantities.TryGetValue(line.OrderItemId, out var qty) || qty <= 0) continue;

            var product = products.FirstOrDefault(p => p.ProductId == line.ProductId);
            if (product is null) continue;
            var unit = product.Units.FirstOrDefault(u => u.UnitId == line.UnitId);
            if (unit is null) continue;

            var baseDelta = qty * Math.Max(unit.Quantity, 1);
            unit.AvailableQuantity += baseDelta;
            product.AvailableQuantity += baseDelta;
            touched.Add(product.ProductId);
        }

        foreach (var productId in touched)
        {
            var product = products.FirstOrDefault(p => p.ProductId == productId);
            if (product is null) continue;
            try { await _idb.UpsertAsync(OfflineStores.Products, product); } catch { }
        }
    }

    private async Task<List<OfflineProductDto>> TryReadProductsForStoreAsync(Guid? storeId)
    {
        try
        {
            return storeId.HasValue
                ? await _idb.GetByIndexAsync<OfflineProductDto>(
                    OfflineStores.Products, OfflineStores.Indexes.ProductsByStore, storeId.Value)
                : await _idb.GetAllAsync<OfflineProductDto>(OfflineStores.Products);
        }
        catch
        {
            return new List<OfflineProductDto>();
        }
    }

    private async Task<(string? nameEn, string? nameAr)> ResolveStoreNamesAsync(Guid? storeId)
    {
        if (!storeId.HasValue) return (null, null);
        if (!string.IsNullOrEmpty(_activeStore.StoreNameEn) || !string.IsNullOrEmpty(_activeStore.StoreNameAr))
            return (_activeStore.StoreNameEn, _activeStore.StoreNameAr);
        try
        {
            var stores = await _idb.GetAllAsync<OfflineStoreDto>(OfflineStores.Stores);
            var match = stores.FirstOrDefault(s => s.StoreId == storeId.Value);
            return (match?.NameEn, match?.NameAr);
        }
        catch { return (null, null); }
    }

    private async Task<(Guid? cashierId, string? cashierName)> ResolveCashierAsync()
    {
        try
        {
            var profiles = await _idb.GetAllAsync<OfflineProfileDto>(OfflineStores.Profile);
            var profile = profiles.FirstOrDefault();
            if (profile is null) return (null, null);
            return (profile.UserId == Guid.Empty ? null : profile.UserId, profile.DisplayName);
        }
        catch { return (null, null); }
    }

    private async Task<Guid?> TryGetActiveShiftIdAsync(Guid? storeId)
    {
        try
        {
            var shifts = await _idb.GetAllAsync<OfflineShiftDto>(OfflineStores.Shifts);
            var match = shifts.FirstOrDefault(s =>
                (s.Status == ShiftStatus.Active.ToString() || s.EndTime is null)
                && (!storeId.HasValue || s.StoreId == storeId));
            return match?.ShiftId;
        }
        catch { return null; }
    }

    private async Task<OrderStatisticsDto> ComputeStatisticsFromCacheAsync(DateTime? fromDate, DateTime? toDate)
    {
        await _activeStore.EnsureLoadedAsync();

        try
        {
            // The cache only holds this cashier's orders (the server pull
            // filters by CashierId), and the server's GetMyStatistics also
            // sums across every store the cashier touched. So we read all
            // cached orders here — scoping to the active store would silently
            // hide orders from a cashier's other assigned stores and make
            // the KPI cards lower than the online values.
            var cached = await _idb.GetAllAsync<OfflineOrderDto>(OfflineStores.Orders);
            var cashierId = (await ResolveCashierAsync()).cashierId;

            IEnumerable<OfflineOrderDto> q = cached;
            // Defensive: if for any reason another cashier's order ended up
            // in this device's cache (account re-login etc.), keep stats to
            // this cashier only — matches the server's WHERE clause.
            if (cashierId.HasValue) q = q.Where(o => !o.CashierId.HasValue || o.CashierId == cashierId);
            if (fromDate.HasValue)  q = q.Where(o => o.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)    q = q.Where(o => o.CreatedAt <= toDate.Value);
            var list = q.ToList();

            // Match Infrastructure.Services.OrderService.GetOrderStatisticsAsync:
            //   CompletedOrders / TotalRevenue → Completed ∪ PartialRefunded
            //   CancelledOrders                → Refunded
            //   TodayOrders / TodayItemsSold   → every order created today (any status)
            //   TodayRevenue                   → today's Completed ∪ PartialRefunded only
            bool IsCompleted(OfflineOrderDto o) =>
                o.Status == OrderStatus.Completed.ToString()
                || o.Status == OrderStatus.PartialRefunded.ToString();
            bool IsCancelled(OfflineOrderDto o) =>
                o.Status == OrderStatus.Refunded.ToString();

            var todayUtc = DateTime.UtcNow.Date;
            var todayAll = list.Where(o => o.CreatedAt.Date == todayUtc).ToList();

            return new OrderStatisticsDto
            {
                TotalOrders = list.Count,
                CompletedOrders = list.Count(IsCompleted),
                CancelledOrders = list.Count(IsCancelled),
                TotalRevenue = list.Where(IsCompleted).Sum(o => o.Total),
                TodayOrders = todayAll.Count,
                TodayRevenue = todayAll.Where(IsCompleted).Sum(o => o.Total),
                TodayItemsSold = todayAll.Sum(o => o.Items.Sum(i => i.Quantity))
            };
        }
        catch
        {
            return new OrderStatisticsDto();
        }
    }

    private static OrderDto MapToOrderDto(OfflineOrderDto src) => new()
    {
        Id = src.OrderId,
        WarehouseId = src.StoreId,
        WarehouseNameEn = src.StoreNameEn,
        WarehouseNameAr = src.StoreNameAr,
        OrderNumber = src.OrderNumber,
        Status = ParseEnum<OrderStatus>(src.Status, OrderStatus.Completed),
        PaymentMethod = ParseEnum<PaymentMethod>(src.PaymentMethod, PaymentMethod.Cash),
        CashAmount = src.CashAmount,
        CardAmount = src.CardAmount,
        Subtotal = src.Subtotal,
        VatRate = src.VatRate,
        VatAmount = src.VatAmount,
        Total = src.Total,
        CreatedAt = src.CreatedAt,
        CashierId = src.CashierId,
        CashierName = src.CashierName,
        ItemCount = src.Items.Count,
        Items = src.Items.Select(i => new OrderItemDto
        {
            Id = i.OrderItemId,
            ProductId = i.ProductId,
            ProductNameEn = i.ProductNameEn,
            ProductNameAr = i.ProductNameAr,
            ProductCode = i.ProductCode,
            UnitId = i.UnitId,
            UnitNameEn = i.UnitNameEn,
            UnitBarcode = i.UnitBarcode,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Total = i.Total
        }).ToList(),
        Refunds = src.Refunds.Select(r => new OrderRefundDto
        {
            Id = r.Id,
            Amount = r.Amount,
            CreatedAt = r.CreatedAt,
            Reason = r.Reason
        }).ToList()
    };

    private static OfflineOrderDto MapServerOrderToOffline(OrderDto src, Guid? storeIdHint) => new()
    {
        OrderId = src.Id,
        StoreId = src.WarehouseId ?? storeIdHint,
        StoreNameEn = src.WarehouseNameEn,
        StoreNameAr = src.WarehouseNameAr,
        OrderNumber = src.OrderNumber,
        Status = src.Status.ToString(),
        PaymentMethod = src.PaymentMethod.ToString(),
        CashAmount = src.CashAmount,
        CardAmount = src.CardAmount,
        Subtotal = src.Subtotal,
        VatRate = src.VatRate,
        VatAmount = src.VatAmount,
        Total = src.Total,
        CreatedAt = src.CreatedAt,
        CashierId = src.CashierId,
        CashierName = src.CashierName,
        Items = src.Items.Select(i => new OfflineOrderItemDto
        {
            OrderItemId = i.Id,
            ProductId = i.ProductId,
            ProductNameEn = i.ProductNameEn,
            ProductNameAr = i.ProductNameAr,
            ProductCode = i.ProductCode,
            UnitId = i.UnitId,
            UnitNameEn = i.UnitNameEn,
            UnitBarcode = i.UnitBarcode,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Total = i.Total
        }).ToList(),
        Refunds = src.Refunds.Select(r => new OfflineOrderRefundDto
        {
            Id = r.Id,
            Amount = r.Amount,
            CreatedAt = r.CreatedAt,
            Reason = r.Reason
        }).ToList()
    };

    private static List<OrderDto> ApplyMyFilters(
        List<OrderDto> source, string? search, OrderStatus? status,
        PaymentMethod? paymentMethod, DateTime? fromDate, DateTime? toDate)
    {
        IEnumerable<OrderDto> q = source;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o => o.OrderNumber.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        if (status.HasValue)        q = q.Where(o => o.Status == status.Value);
        if (paymentMethod.HasValue) q = q.Where(o => o.PaymentMethod == paymentMethod.Value);
        if (fromDate.HasValue)      q = q.Where(o => o.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)        q = q.Where(o => o.CreatedAt <= toDate.Value);
        return q.ToList();
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
    {
        return Enum.TryParse<T>(value, true, out var v) ? v : fallback;
    }
}
