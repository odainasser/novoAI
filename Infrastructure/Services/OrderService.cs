using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Orders;
using Application.Services;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class OrderService : IOrderService
{
    private const decimal VatRate = 0.05m;

    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INumberSequenceService _numberSequence;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        ApplicationDbContext context,
        IEmailService emailService,
        INotificationService notificationService,
        UserManager<ApplicationUser> userManager,
        INumberSequenceService numberSequence,
        ILogger<OrderService> logger)
    {
        _context = context;
        _emailService = emailService;
        _notificationService = notificationService;
        _userManager = userManager;
        _numberSequence = numberSequence;
        _logger = logger;
    }

    public async Task<PaginatedList<OrderDto>> GetAllOrdersAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        OrderStatus? status = null,
        OrderChannel? channel = null,
        PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? warehouseId = null,
        IEnumerable<Guid>? warehouseIds = null)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Refunds).ThenInclude(r => r.Items)
            .AsQueryable();

        query = ApplyFilters(query, search, status, channel, paymentMethod, fromDate, toDate);

        // Filter by store/warehouse directly on the order
        if (warehouseId.HasValue)
        {
            query = query.Where(o => o.WarehouseId == warehouseId.Value);
        }

        // Multi-warehouse filter (used by Branch Panel — a branch typically has
        // a store + a back-office warehouse and we want both in scope). The list
        // is cast to Guid? so EF translates this as `WHERE WarehouseId IN (...)`
        // without touching .Value (which has tripped EF translation in the past).
        if (warehouseIds is not null)
        {
            var ids = warehouseIds.Select(g => (Guid?)g).ToList();
            if (ids.Count == 0)
            {
                // Caller explicitly passed an empty list — return empty.
                return new PaginatedList<OrderDto>(new List<OrderDto>(), 0, pageNumber, pageSize);
            }
            query = query.Where(o => ids.Contains(o.WarehouseId));
        }

        query = query.OrderByDescending(o => o.CreatedAt);

        var count = await query.CountAsync();

        var orders = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var orderDtos = orders.Select(MapToDto).ToList();

        return new PaginatedList<OrderDto>(orderDtos, count, pageNumber, pageSize);
    }

    private IQueryable<Order> ApplyFilters(
        IQueryable<Order> query,
        string? search,
        OrderStatus? status,
        OrderChannel? channel,
        PaymentMethod? paymentMethod,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(searchLower) ||
                (o.CustomerName != null && o.CustomerName.ToLower().Contains(searchLower)) ||
                (o.CustomerEmail != null && o.CustomerEmail.ToLower().Contains(searchLower)) ||
                (o.CustomerPhone != null && o.CustomerPhone.Contains(search)));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (channel.HasValue)
        {
            query = query.Where(o => o.Channel == channel.Value);
        }

        if (paymentMethod.HasValue)
        {
            query = query.Where(o => o.PaymentMethod == paymentMethod.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }

        return query;
    }

    public async Task<OrderDto?> GetOrderByIdAsync(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Refunds).ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderDto?> GetOrderByNumberAsync(string orderNumber)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Refunds).ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

        return order == null ? null : MapToDto(order);
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, string? createdByName = null)
    {
        // Idempotency: if this exact sale was already created (e.g. a double-submit),
        // return the existing order instead of creating a duplicate.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Refunds).ThenInclude(r => r.Items)
                .FirstOrDefaultAsync(o => o.IdempotencyKey == request.IdempotencyKey);
            if (existing != null)
            {
                _logger.LogInformation("Duplicate order submission ignored (idempotency key {Key}); returning existing order {OrderNumber}", request.IdempotencyKey, existing.OrderNumber);
                return MapToDto(existing);
            }
        }

        // Validate products and get their details
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync();

        if (products.Count != productIds.Count)
        {
            throw new InvalidOperationException("One or more products not found or inactive");
        }

        // POS orders are sold from a single store: stock must come from (and be
        // validated against) that warehouse only. Resolve it now so we can validate
        // per-warehouse before generating an order number.
        var isPos = request.Channel == OrderChannel.POS;
        Warehouse? warehouse = request.WarehouseId.HasValue
            ? await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId.Value)
            : null;

        if (isPos && warehouse == null)
        {
            throw new InvalidOperationException("A store/warehouse is required for POS orders.");
        }

        // Check stock availability from StockBalance (stock is stored in base units).
        // POS: validate against the order's warehouse only.
        // Online/other: validate aggregate stock across all warehouses (FIFO fulfillment).
        foreach (var item in request.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == item.UnitId && u.IsActive);
            var baseQtyRequired = item.Quantity * (unit?.Quantity ?? 1);

            int available;
            if (isPos)
            {
                available = await _context.StockBalances
                    .Where(sb => sb.UnitId == item.UnitId && sb.WarehouseId == warehouse!.Id)
                    .Select(sb => (int?)sb.AvailableQuantity)
                    .FirstOrDefaultAsync() ?? 0;
            }
            else
            {
                available = await _context.StockBalances
                    .Where(sb => sb.UnitId == item.UnitId)
                    .SumAsync(sb => sb.AvailableQuantity);
            }

            if (available < baseQtyRequired)
            {
                throw new InvalidOperationException($"Insufficient stock for product: {product.NameEn}");
            }
        }

        // Generate order number
        var orderNumber = await GenerateOrderNumberAsync();

        // Pre-load all referenced units
        var unitIds = request.Items
            .Select(i => i.UnitId)
            .Distinct()
            .ToList();

        var units = await _context.Units
            .Include(su => su.UnitOfMeasure)
            .Where(su => unitIds.Contains(su.Id) && su.IsActive)
            .ToListAsync();

        // Calculate totals
        decimal subtotal = 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            var product = products.First(p => p.Id == item.ProductId);

            // Resolve price from unit
            var unit = units.FirstOrDefault(su => su.Id == item.UnitId && su.ProductId == product.Id)
                ?? throw new InvalidOperationException($"Unit not found or inactive for product '{product.NameEn}'");

            decimal unitPrice = unit.SellingPrice;
            string? unitNameEn = unit.UnitOfMeasure?.NameEn;
            string? unitNameAr = unit.UnitOfMeasure?.NameAr;
            string? unitBarcode = unit.Barcode;

            var itemTotal = unitPrice * item.Quantity;
            subtotal += itemTotal;

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                UnitId = unit?.Id,
                ProductNameEn = product.NameEn,
                ProductNameAr = product.NameAr,
                ProductCode = product.Code,
                UnitNameEn = unitNameEn,
                UnitNameAr = unitNameAr,
                UnitBarcode = unitBarcode,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                Total = itemTotal
            });
        }

        var vatAmount = subtotal * VatRate;
        var total = subtotal + vatAmount;

        // Store split payment amounts (UI already validates sum == total)
        decimal? cashAmount = null;
        decimal? cardAmount = null;
        if (request.PaymentMethod == PaymentMethod.Split)
        {
            if (!request.CashAmount.HasValue || !request.CardAmount.HasValue)
                throw new InvalidOperationException("Both CashAmount and CardAmount are required for split payments.");
            if (request.CashAmount.Value < 0 || request.CardAmount.Value < 0)
                throw new InvalidOperationException("Split payment amounts cannot be negative.");
            cashAmount = request.CashAmount.Value;
            cardAmount = request.CardAmount.Value;
        }

        var saleTime = request.ClientCreatedAt ?? DateTime.UtcNow;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey,
            Channel = request.Channel,
            Status = OrderStatus.Completed, // POS orders are completed immediately
            PaymentMethod = request.PaymentMethod,
            CashAmount = cashAmount,
            CardAmount = cardAmount,
            Subtotal = subtotal,
            VatRate = VatRate,
            VatAmount = vatAmount,
            Total = total,
            WarehouseId = warehouse?.Id,
            WarehouseNameEn = warehouse?.NameEn,
            WarehouseNameAr = warehouse?.NameAr,
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            Notes = request.Notes,
            CreatedAt = saleTime,
            CompletedAt = saleTime,
            Items = orderItems
        };

        // Persist order + stock deduction + inventory history atomically.
        // Wrap in the execution strategy so transient failures retry the whole unit
        // (required because EnableRetryOnFailure is configured on the DbContext).
        async Task ApplyOrderAsync()
        {
            _context.Orders.Add(order);

            // Deduct stock from StockBalance and create InventoryHistory records.
            // POS orders must deduct from the order's warehouse only; online/other
            // orders may draw from any warehouse via FIFO fallback.
            foreach (var item in orderItems)
            {
                await DeductStockForSaleAsync(item.UnitId!.Value, item.Quantity, order.Id, order.OrderNumber, item.ProductNameEn, createdByName, order.WarehouseId, restrictToPreferredWarehouse: isPos);
            }

            await _context.SaveChangesAsync();
        }

        try
        {
            await ConcurrencyRetry.ExecuteWithRetryAsync(_context, ApplyOrderAsync,
                "Stock changed during checkout and the operation could not be completed after several attempts. Please retry.");
        }
        catch (DbUpdateException ex) when (!string.IsNullOrWhiteSpace(request.IdempotencyKey) && IsDuplicateIdempotencyKey(ex))
        {
            // A concurrent request carrying the same idempotency key won the
            // insert race. Return that order rather than surfacing an error.
            _context.ChangeTracker.Clear();
            var winner = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Refunds).ThenInclude(r => r.Items)
                .FirstOrDefaultAsync(o => o.IdempotencyKey == request.IdempotencyKey);
            if (winner == null) throw;
            return MapToDto(winner);
        }

        // Check low-stock levels for the ordered selling units at this store and notify admins.
        await NotifyLowStockAsync(order);

        return MapToDto(order);
    }

    /// <summary>
    /// After a sale, check each ordered selling unit at the order's store.
    /// If a unit's remaining stock at that store has reached or fallen below its
    /// LowStockThreshold, email and notify every active administrator. Online/multi-warehouse
    /// orders are skipped — this check is per-store. Failures are logged only so they never
    /// break order creation.
    /// </summary>
    private async Task NotifyLowStockAsync(Order order)
    {
        try
        {
            // Per-store check applies only when the order is bound to a single warehouse.
            if (order.WarehouseId == null) return;
            var warehouseId = order.WarehouseId.Value;

            var orderedUnitIds = order.Items
                .Where(i => i.UnitId.HasValue)
                .Select(i => i.UnitId!.Value)
                .Distinct()
                .ToList();
            if (orderedUnitIds.Count == 0) return;

            // Pull each ordered selling unit's remaining stock at this warehouse, plus its
            // own threshold and product info. Left-join on StockBalance so a missing row
            // counts as 0 remaining.
            var unitStock = await (from u in _context.Units
                                   join p in _context.Products on u.ProductId equals p.Id
                                   join l in _context.Lookups on u.UnitOfMeasureId equals l.Id into uomJoin
                                   from uom in uomJoin.DefaultIfEmpty()
                                   join sb in _context.StockBalances.Where(s => s.WarehouseId == warehouseId)
                                       on u.Id equals sb.UnitId into sbJoin
                                   from sb in sbJoin.DefaultIfEmpty()
                                   where orderedUnitIds.Contains(u.Id)
                                   select new
                                   {
                                       ProductId = p.Id,
                                       ProductNameEn = p.NameEn,
                                       ProductNameAr = p.NameAr,
                                       ProductCode = p.Code,
                                       UnitId = u.Id,
                                       UnitNameEn = uom != null ? uom.NameEn : null,
                                       UnitNameAr = uom != null ? uom.NameAr : null,
                                       Barcode = u.Barcode,
                                       Remaining = sb != null ? (int?)sb.AvailableQuantity : null,
                                       Threshold = u.LowStockThreshold
                                   })
                                  .ToListAsync();

            var lowUnits = unitStock
                .Select(x => new
                {
                    x.ProductId,
                    x.ProductNameEn,
                    x.ProductNameAr,
                    x.ProductCode,
                    x.UnitId,
                    x.UnitNameEn,
                    x.UnitNameAr,
                    x.Barcode,
                    Remaining = x.Remaining ?? 0,
                    x.Threshold
                })
                .Where(x => x.Remaining <= x.Threshold)
                .ToList();

            if (lowUnits.Count == 0)
            {
                _logger.LogInformation(
                    "Low-stock check (warehouse {WarehouseId}): no ordered units at or below threshold",
                    warehouseId);
                return;
            }

            // One alert item per product, with the affected selling units listed beneath.
            var lowItems = lowUnits
                .GroupBy(x => new { x.ProductId, x.ProductNameEn, x.ProductNameAr, x.ProductCode })
                .Select(g => new LowStockAlertItem
                {
                    ProductNameEn = g.Key.ProductNameEn,
                    ProductNameAr = g.Key.ProductNameAr,
                    ProductCode = g.Key.ProductCode,
                    RemainingQuantity = g.Sum(x => x.Remaining),
                    LowStockThreshold = g.Sum(x => x.Threshold),
                    Units = g.OrderBy(x => x.UnitNameEn)
                            .Select(x => new LowStockAlertUnitItem
                            {
                                UnitNameEn = string.IsNullOrWhiteSpace(x.UnitNameEn) ? "Unit" : x.UnitNameEn!,
                                UnitNameAr = string.IsNullOrWhiteSpace(x.UnitNameAr) ? "وحدة" : x.UnitNameAr!,
                                Barcode = x.Barcode ?? string.Empty,
                                RemainingQuantity = x.Remaining,
                                LowStockThreshold = x.Threshold
                            })
                            .ToList()
                })
                .ToList();

            // Recipients: all active admins.
            var recipientEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var recipientUserIds = new HashSet<Guid>();

            var admins = await _userManager.GetUsersInRoleAsync(Roles.Administrator);
            foreach (var a in admins)
            {
                if (a.IsActive && !a.IsDeleted)
                {
                    recipientUserIds.Add(a.Id);
                    if (!string.IsNullOrWhiteSpace(a.Email))
                        recipientEmails.Add(a.Email!);
                }
            }

            if (recipientEmails.Count == 0 && recipientUserIds.Count == 0)
            {
                _logger.LogWarning(
                    "Low-stock alert at warehouse {WarehouseId}: {UnitCount} affected unit(s) but no recipients",
                    warehouseId, lowUnits.Count);
                return;
            }

            _logger.LogInformation(
                "Low-stock alert: warehouse={WarehouseId} ({WarehouseName}) affected_units={UnitCount} email_recipients={EmailRecipientCount} app_recipients={AppRecipientCount}",
                warehouseId, order.WarehouseNameEn, lowUnits.Count, recipientEmails.Count, recipientUserIds.Count);

            // Real-time in-app notification (always — even if recipients have no email)
            await PublishLowStockNotificationAsync(
                recipientUserIds,
                lowItems,
                order.WarehouseNameEn,
                order.WarehouseNameAr);

            // Email (only if recipients have addresses)
            if (recipientEmails.Count > 0)
            {
                await _emailService.SendLowStockAlertAsync(
                    recipientEmails,
                    lowItems,
                    null,
                    order.WarehouseNameEn,
                    order.WarehouseNameAr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Low-stock notification processing failed");
        }
    }

    private async Task PublishLowStockNotificationAsync(
        IEnumerable<Guid> userIds,
        List<LowStockAlertItem> items,
        string? warehouseNameEn,
        string? warehouseNameAr)
    {
        try
        {
            if (items.Count == 0) return;

            var atEn = string.IsNullOrWhiteSpace(warehouseNameEn) ? string.Empty : $" at {warehouseNameEn}";
            var atAr = string.IsNullOrWhiteSpace(warehouseNameAr) ? string.Empty : $" في {warehouseNameAr}";

            string titleEn, titleAr, bodyEn, bodyAr;
            if (items.Count == 1)
            {
                var first = items[0];
                titleEn = $"Low stock: {first.ProductNameEn}{atEn}";
                titleAr = $"انخفاض المخزون: {first.ProductNameAr}{atAr}";
                bodyEn = $"Remaining {first.RemainingQuantity} (threshold {first.LowStockThreshold}).";
                bodyAr = $"المتبقي {first.RemainingQuantity} (الحد {first.LowStockThreshold}).";
            }
            else
            {
                titleEn = $"Low stock: {items.Count} products{atEn}";
                titleAr = $"انخفاض المخزون: {items.Count} منتجات{atAr}";
                var firstNames = string.Join(", ", items.Take(3).Select(i => i.ProductNameEn));
                var firstNamesAr = string.Join(", ", items.Take(3).Select(i => i.ProductNameAr));
                bodyEn = $"Affected: {firstNames}{(items.Count > 3 ? "…" : string.Empty)}";
                bodyAr = $"المتأثرة: {firstNamesAr}{(items.Count > 3 ? "…" : string.Empty)}";
            }

            await _notificationService.SendBulkAsync(
                userIds,
                NotificationType.LowStock,
                titleEn,
                titleAr,
                bodyEn,
                bodyAr,
                link: "/admin/inventory/balances?filter=lowstock");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish low-stock notification");
        }
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Refunds).ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            throw new InvalidOperationException("Order not found");
        }

        order.Status = request.Status;

        if (request.Status == OrderStatus.Completed)
        {
            order.CompletedAt = DateTime.UtcNow;
        }
        else if (request.Status == OrderStatus.Refunded)
        {
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = request.CancellationReason;
        }

        await _context.SaveChangesAsync();

        return MapToDto(order);
    }

    public async Task<bool> CancelOrderAsync(Guid id, string? reason)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return false;
        }

        if (order.Status == OrderStatus.Refunded)
        {
            return true; // Already refunded
        }

        bool result = false;
        async Task ApplyCancelAsync()
        {
        // If the order was partially refunded, treat the remaining as another partial refund
        // and then mark the order as fully refunded
        if (order.Status == OrderStatus.PartialRefunded)
        {
            var remainingItems = order.Items.Where(i => i.Quantity > 0).ToList();
            if (remainingItems.Any())
            {
                decimal vatRate = order.VatRate;
                decimal refundAmount = 0m;

                // Create OrderRefund record for the remaining items
                var refund = new OrderRefund
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    CreatedAt = DateTime.UtcNow,
                    Reason = reason ?? "Refund of remaining items"
                };

                foreach (var item in remainingItems)
                {
                    var itemVat = item.UnitPrice * vatRate;
                    var itemTotalRefund = (item.UnitPrice + itemVat) * item.Quantity;
                    refundAmount += itemTotalRefund;

                    refund.Items.Add(new OrderRefundItem
                    {
                        Id = Guid.NewGuid(),
                        OrderRefundId = refund.Id,
                        OrderItemId = item.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Total = item.UnitPrice * item.Quantity
                    });

                    // Restore stock for refunded items
                    await RestoreStockForReturnAsync(item.UnitId!.Value, item.Quantity, order.Id, order.OrderNumber, item.ProductNameEn, null, order.WarehouseId);

                    // Zero out the item quantity
                    item.Quantity = 0;
                    item.Total = 0;
                }

                refund.Amount = refundAmount;
                _context.OrderRefunds.Add(refund);

                // Recalculate order totals
                order.Subtotal = 0;
                order.VatAmount = 0;
                order.Total = 0;
            }

            order.Status = OrderStatus.Refunded;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = reason;

            await _context.SaveChangesAsync();
            result = true;
            return;
        }

        order.Status = OrderStatus.Refunded;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = reason;

        // Restore stock to StockBalance and create InventoryHistory records
        foreach (var item in order.Items)
        {
            await RestoreStockForReturnAsync(item.UnitId!.Value, item.Quantity, order.Id, order.OrderNumber, item.ProductNameEn, null, order.WarehouseId);
        }

        await _context.SaveChangesAsync();
        result = true;
        }

        try
        {
            await ConcurrencyRetry.ExecuteInTransactionAsync(_context, ApplyCancelAsync);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("A concurrent update was detected on stock balances. Please retry the operation.");
        }

        return result;
    }

    public async Task<bool> PartialRefundAsync(Guid id, List<Application.Services.PartialRefundItem> items, DateTime? clientCreatedAt = null)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return false;

        if (order.Status == Domain.Enums.OrderStatus.Refunded)
        {
            return true; // already refunded
        }

        if (items == null || !items.Any())
        {
            // If no items provided, treat as full refund
            return await CancelOrderAsync(id, "Partial refund converted to full refund");
        }

        async Task ApplyPartialRefundAsync()
        {
        // Validate items and compute total refund amount
        decimal refundAmount = 0m;
        decimal vatRate = order.VatRate;

        var refundedQuantities = new Dictionary<Guid, int>(); // orderItemId → qty refunded

        foreach (var item in items)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.Id == item.OrderItemId);
            if (orderItem == null) continue;
            var qty = Math.Min(item.Quantity, orderItem.Quantity);
            if (qty <= 0) continue;

            refundedQuantities[orderItem.Id] = qty;

            var itemUnitVat = orderItem.UnitPrice * vatRate;
            var itemTotalRefund = (orderItem.UnitPrice + itemUnitVat) * qty;
            refundAmount += itemTotalRefund;

            // Reduce the order item quantity and total
            orderItem.Quantity -= qty;
            orderItem.Total = orderItem.UnitPrice * orderItem.Quantity;
        }

        // Recalculate order totals from remaining items
        var newSubtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        var newVatAmount = Math.Round(newSubtotal * vatRate, 2);
        var newTotal = newSubtotal + newVatAmount;

        order.Subtotal = newSubtotal;
        order.VatAmount = newVatAmount;
        order.Total = newTotal;

        // Create OrderRefund record
        var refund = new OrderRefund
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Amount = refundAmount,
            CreatedAt = clientCreatedAt ?? DateTime.UtcNow,
            Reason = "Partial refund"
        };

        foreach (var item in items)
        {
            var orderItem = order.Items.FirstOrDefault(i => i.Id == item.OrderItemId);
            if (orderItem == null) continue;
            if (!refundedQuantities.TryGetValue(orderItem.Id, out var qty) || qty <= 0) continue;

            refund.Items.Add(new OrderRefundItem
            {
                Id = Guid.NewGuid(),
                OrderRefundId = refund.Id,
                OrderItemId = orderItem.Id,
                ProductId = orderItem.ProductId,
                Quantity = qty,
                UnitPrice = orderItem.UnitPrice,
                Total = orderItem.UnitPrice * qty
            });
        }

        _context.OrderRefunds.Add(refund);

        // Restore stock to StockBalance for refunded items
        foreach (var refundItem in refund.Items)
        {
            var orderItem = order.Items.First(i => i.Id == refundItem.OrderItemId);
            await RestoreStockForReturnAsync(orderItem.UnitId!.Value, refundItem.Quantity, order.Id, order.OrderNumber, orderItem.ProductNameEn, null, order.WarehouseId);
        }

        // If no items left, mark as fully refunded; otherwise partial
        if (!order.Items.Any(i => i.Quantity > 0))
        {
            order.Status = OrderStatus.Refunded;
        }
        else
        {
            order.Status = OrderStatus.PartialRefunded;
        }

        await _context.SaveChangesAsync();
        }

        try
        {
            await ConcurrencyRetry.ExecuteInTransactionAsync(_context, ApplyPartialRefundAsync);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("A concurrent update was detected on stock balances. Please retry the operation.");
        }

        return true;
    }

    /// <summary>
    /// Wraps a transactional unit of work in the configured execution strategy so transient
    /// failures retry the whole transaction (required when EnableRetryOnFailure is configured).
    /// If a caller-managed transaction is already active, the work is run inline without nesting.
    /// </summary>
    private static bool IsDuplicateIdempotencyKey(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        // Atomic per-day counter — concurrency-safe, unlike the old
        // "select max(OrderNumber) + 1" which could hand two simultaneous
        // checkouts the same number and fail on the unique index.
        var prefix = $"ORD-{DateTime.UtcNow:yyyyMMdd}";
        var sequence = await _numberSequence.NextAsync(prefix);
        return $"{prefix}-{sequence:D4}";
    }

    public async Task<OrderStatisticsDto> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Orders.AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }

        // Use database aggregation for main statistics
        var stats = await query.GroupBy(o => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                CompletedOrders = g.Count(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.PartialRefunded),
                CancelledOrders = g.Count(o => o.Status == OrderStatus.Refunded),
                TotalRevenue = g.Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.PartialRefunded).Sum(o => o.Total)
            })
            .FirstOrDefaultAsync();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Separate query for today's statistics
        var todayQuery = _context.Orders.Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);

        var todayStats = await todayQuery.GroupBy(o => 1)
            .Select(g => new
            {
                TodayOrders = g.Count(),
                TodayRevenue = g.Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.PartialRefunded).Sum(o => o.Total)
            })
            .FirstOrDefaultAsync();

        var todayItemsSold = await todayQuery
            .SelectMany(o => o.Items)
            .SumAsync(i => i.Quantity);

        return new OrderStatisticsDto
        {
            TotalOrders = stats?.TotalOrders ?? 0,
            CompletedOrders = stats?.CompletedOrders ?? 0,
            CancelledOrders = stats?.CancelledOrders ?? 0,
            PendingOrders = 0,
            TotalRevenue = stats?.TotalRevenue ?? 0,
            TodayRevenue = todayStats?.TodayRevenue ?? 0,
            TodayOrders = todayStats?.TodayOrders ?? 0,
            TodayItemsSold = todayItemsSold
        };
    }

    public async Task<byte[]> ExportOrdersToExcelAsync(
        string? search = null,
        OrderStatus? status = null,
        OrderChannel? channel = null,
        PaymentMethod? paymentMethod = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? warehouseId = null,
        bool isArabic = false,
        IEnumerable<Guid>? warehouseIds = null)
    {
        var query = _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Refunds).ThenInclude(r => r.Items)
            .AsQueryable();

        query = ApplyFilters(query, search, status, channel, paymentMethod, fromDate, toDate);

        if (warehouseId.HasValue)
        {
            query = query.Where(o => o.WarehouseId == warehouseId.Value);
        }

        // Multi-warehouse filter (mirrors GetAllOrdersAsync) — used by the Branch Panel
        // to scope export to all warehouses owned by a branch.
        if (warehouseIds is not null)
        {
            var ids = warehouseIds.Select(g => (Guid?)g).ToList();
            if (ids.Count == 0)
            {
                // Caller explicitly passed an empty list — short-circuit to an empty workbook.
                using var emptyBook = new ClosedXML.Excel.XLWorkbook();
                emptyBook.Worksheets.Add("Orders");
                using var emptyStream = new MemoryStream();
                emptyBook.SaveAs(emptyStream);
                return emptyStream.ToArray();
            }
            query = query.Where(o => ids.Contains(o.WarehouseId));
        }

        query = query.OrderByDescending(o => o.CreatedAt);

        var rows = await query
            .Select(order => new
            {
                order.OrderNumber,
                order.CreatedAt,
                order.PaymentMethod,
                order.Status,
                order.WarehouseNameEn,
                order.WarehouseNameAr,
                ItemCount = order.Items.Sum(i => (int?)i.Quantity) ?? 0,
                order.Subtotal,
                order.VatAmount,
                order.Total
            })
            .ToListAsync();

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Orders");

        // Header
        var headers = new[]
        {
            isArabic ? "رقم الطلب" : "Order Number",
            isArabic ? "تاريخ الطلب" : "Order Date",
            isArabic ? "طريقة الدفع" : "Payment Method",
            isArabic ? "الحالة" : "Status",
            isArabic ? "المتجر" : "Store",
            isArabic ? "العناصر" : "Items",
            isArabic ? "المجموع الفرعي" : "Subtotal",
            isArabic ? "ضريبة القيمة المضافة" : "VAT",
            isArabic ? "الإجمالي" : "Total"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromArgb(0xF3, 0xF4, 0xF6);
        headerRow.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Left;

        var rowIndex = 2;
        foreach (var r in rows)
        {
            sheet.Cell(rowIndex, 1).Value = r.OrderNumber;
            sheet.Cell(rowIndex, 2).Value = r.CreatedAt;
            sheet.Cell(rowIndex, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
            sheet.Cell(rowIndex, 3).Value = r.PaymentMethod.ToString();
            sheet.Cell(rowIndex, 4).Value = r.Status.ToString();
            sheet.Cell(rowIndex, 5).Value = isArabic ? (r.WarehouseNameAr ?? string.Empty) : (r.WarehouseNameEn ?? string.Empty);
            sheet.Cell(rowIndex, 6).Value = r.ItemCount;
            sheet.Cell(rowIndex, 7).Value = r.Subtotal;
            sheet.Cell(rowIndex, 7).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(rowIndex, 8).Value = r.VatAmount;
            sheet.Cell(rowIndex, 8).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(rowIndex, 9).Value = r.Total;
            sheet.Cell(rowIndex, 9).Style.NumberFormat.Format = "#,##0.00";
            rowIndex++;
        }

        sheet.RangeUsed()?.SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        // Cap any over-eager auto-fit so a long product/store name doesn't make the sheet unreadable.
        foreach (var col in sheet.ColumnsUsed())
        {
            if (col.Width > 50) col.Width = 50;
            if (col.Width < 12) col.Width = 12;
        }

        if (isArabic)
        {
            sheet.RightToLeft = true;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Deducts stock from StockBalance and creates InventoryHistory audit records.
    /// When <paramref name="restrictToPreferredWarehouse"/> is true (POS), only the
    /// preferred warehouse is touched and an exception is thrown if it cannot cover
    /// the full quantity. Otherwise the preferred warehouse is drained first and any
    /// shortfall is taken from other warehouses by FIFO of CreatedAt.
    /// </summary>
    private async Task DeductStockForSaleAsync(Guid unitId, int quantity, Guid orderId, string orderNumber, string productName, string? performedBy, Guid? preferredWarehouseId = null, bool restrictToPreferredWarehouse = false)
    {
        // Convert order quantity to base units (e.g., 2 cartons × 12 = 24 pieces)
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == unitId);
        var baseQuantity = quantity * (unit?.Quantity ?? 1);
        var remaining = baseQuantity;

        if (restrictToPreferredWarehouse && !preferredWarehouseId.HasValue)
            throw new InvalidOperationException("Preferred warehouse is required for warehouse-restricted stock deduction.");

        IQueryable<StockBalance> query = _context.StockBalances
            .Where(sb => sb.UnitId == unitId && sb.AvailableQuantity > 0);

        if (restrictToPreferredWarehouse)
        {
            query = query.Where(sb => sb.WarehouseId == preferredWarehouseId!.Value);
        }

        var stockBalances = await query
            .OrderByDescending(sb => preferredWarehouseId.HasValue && sb.WarehouseId == preferredWarehouseId.Value)
            .ThenBy(sb => sb.CreatedAt)
            .ToListAsync();

        foreach (var sb in stockBalances)
        {
            if (remaining <= 0) break;

            var deduct = Math.Min(remaining, sb.AvailableQuantity);
            var beforeQty = sb.AvailableQuantity;

            sb.AvailableQuantity -= deduct;
            sb.UpdatedAt = DateTime.UtcNow;
            remaining -= deduct;

            _context.InventoryHistories.Add(new InventoryHistory
            {
                Id = Guid.NewGuid(),
                WarehouseId = sb.WarehouseId,
                UnitId = unitId,
                ActionType = InventoryActionType.Sale,
                QuantityChange = -deduct,
                AvailableQuantityBefore = beforeQty,
                AvailableQuantityAfter = sb.AvailableQuantity,
                ReferenceType = "Order",
                ReferenceId = orderId,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow,
                Notes = $"Order {orderNumber} - {productName}",
                CreatedAt = DateTime.UtcNow
            });
        }

        // If we couldn't fully cover the sale (concurrent oversell or stock changed
        // between validation and deduction), throw so the surrounding transaction rolls back.
        if (remaining > 0)
        {
            throw new InvalidOperationException(
                $"Insufficient stock for {productName} at fulfilment time. Short by {remaining} base units.");
        }
    }

    /// <summary>
    /// Restores stock to StockBalance and creates InventoryHistory audit records.
    /// Reverses the original sale's per-warehouse deductions (recorded in
    /// InventoryHistory) so the stock returns to the warehouses it was taken from.
    /// Falls back to the order's store warehouse (or the first active warehouse) only
    /// if no original sale history can be found for this order/unit.
    /// </summary>
    private async Task RestoreStockForReturnAsync(Guid unitId, int quantity, Guid orderId, string orderNumber, string productName, string? performedBy, Guid? preferredWarehouseId = null)
    {
        // Convert order quantity to base units (e.g., 2 cartons × 12 = 24 pieces)
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == unitId);
        var baseQuantity = quantity * (unit?.Quantity ?? 1);

        if (baseQuantity <= 0) return;

        // Compute the net per-warehouse deduction for this (order, unit) so we can
        // reverse partial refunds correctly: net = sum(Sale) + sum(Return) — Sale
        // entries are negative, prior Return entries are positive.
        var historyAggregates = await _context.InventoryHistories
            .Where(h => h.ReferenceType == "Order"
                        && h.ReferenceId == orderId
                        && h.UnitId == unitId
                        && (h.ActionType == InventoryActionType.Sale || h.ActionType == InventoryActionType.Return))
            .GroupBy(h => h.WarehouseId)
            .Select(g => new { WarehouseId = g.Key, Net = g.Sum(x => x.QuantityChange) })
            .ToListAsync();

        // Net deducted (still owed to the warehouse) is -Net where Net is negative.
        var deductionsRemaining = historyAggregates
            .Where(a => a.Net < 0)
            .Select(a => new { a.WarehouseId, NetDeducted = -a.Net })
            .OrderByDescending(a => preferredWarehouseId.HasValue && a.WarehouseId == preferredWarehouseId.Value)
            .ThenBy(a => a.NetDeducted)
            .ToList();

        var remaining = baseQuantity;

        foreach (var entry in deductionsRemaining)
        {
            if (remaining <= 0) break;

            var restoreQty = Math.Min(remaining, entry.NetDeducted);

            var sb = await _context.StockBalances
                .FirstOrDefaultAsync(s => s.WarehouseId == entry.WarehouseId && s.UnitId == unitId);

            if (sb == null)
            {
                sb = new StockBalance
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = entry.WarehouseId,
                    UnitId = unitId,
                    AvailableQuantity = 0,
                    ReservedQuantity = 0,
                    InTransitQuantity = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StockBalances.Add(sb);
            }

            var beforeQty = sb.AvailableQuantity;
            sb.AvailableQuantity += restoreQty;
            sb.UpdatedAt = DateTime.UtcNow;
            remaining -= restoreQty;

            _context.InventoryHistories.Add(new InventoryHistory
            {
                Id = Guid.NewGuid(),
                WarehouseId = sb.WarehouseId,
                UnitId = unitId,
                ActionType = InventoryActionType.Return,
                QuantityChange = restoreQty,
                AvailableQuantityBefore = beforeQty,
                AvailableQuantityAfter = sb.AvailableQuantity,
                ReferenceType = "Order",
                ReferenceId = orderId,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow,
                Notes = $"Refund - Order {orderNumber} - {productName}",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (remaining <= 0) return;

        // Fallback: no matching sale history (legacy data or unexpected state). Restore
        // the leftover to the order's store warehouse, or the first active warehouse.
        Guid fallbackWarehouseId;
        if (preferredWarehouseId.HasValue)
        {
            fallbackWarehouseId = preferredWarehouseId.Value;
        }
        else
        {
            var warehouse = await _context.Warehouses
                .Where(w => w.IsActive)
                .OrderBy(w => w.CreatedAt)
                .FirstOrDefaultAsync();
            if (warehouse == null) return;
            fallbackWarehouseId = warehouse.Id;
        }

        var fallback = await _context.StockBalances
            .FirstOrDefaultAsync(s => s.WarehouseId == fallbackWarehouseId && s.UnitId == unitId);

        if (fallback == null)
        {
            fallback = new StockBalance
            {
                Id = Guid.NewGuid(),
                WarehouseId = fallbackWarehouseId,
                UnitId = unitId,
                AvailableQuantity = 0,
                ReservedQuantity = 0,
                InTransitQuantity = 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockBalances.Add(fallback);
        }

        var fallbackBefore = fallback.AvailableQuantity;
        fallback.AvailableQuantity += remaining;
        fallback.UpdatedAt = DateTime.UtcNow;

        _context.InventoryHistories.Add(new InventoryHistory
        {
            Id = Guid.NewGuid(),
            WarehouseId = fallback.WarehouseId,
            UnitId = unitId,
            ActionType = InventoryActionType.Return,
            QuantityChange = remaining,
            AvailableQuantityBefore = fallbackBefore,
            AvailableQuantityAfter = fallback.AvailableQuantity,
            ReferenceType = "Order",
            ReferenceId = orderId,
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow,
            Notes = $"Refund - Order {orderNumber} - {productName}",
            CreatedAt = DateTime.UtcNow
        });
    }

    private OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Channel = order.Channel,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            CashAmount = order.CashAmount,
            CardAmount = order.CardAmount,
            Subtotal = order.Subtotal,
            VatRate = order.VatRate,
            VatAmount = order.VatAmount,
            Total = order.Total,
            WarehouseId = order.WarehouseId,
            WarehouseNameEn = order.WarehouseNameEn,
            WarehouseNameAr = order.WarehouseNameAr,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            Notes = order.Notes,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            CancellationReason = order.CancellationReason,
            ItemCount = order.Items.Count,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductNameEn = i.ProductNameEn,
                ProductNameAr = i.ProductNameAr,
                ProductCode = i.ProductCode,
                UnitId = i.UnitId,
                UnitNameEn = i.UnitNameEn,
                UnitNameAr = i.UnitNameAr,
                UnitBarcode = i.UnitBarcode,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Total = i.Total
            }).ToList()
            ,
            Refunds = order.Refunds
                .Select(r => new Application.Features.Orders.OrderRefundDto
                {
                    Id = r.Id,
                    Amount = r.Amount,
                    CreatedAt = r.CreatedAt,
                    Reason = r.Reason,
                    Items = r.Items.Select(ri => new Application.Features.Orders.OrderRefundItemDto
                    {
                        Id = ri.Id,
                        OrderItemId = ri.OrderItemId,
                        ProductId = ri.ProductId,
                        Quantity = ri.Quantity,
                        UnitPrice = ri.UnitPrice,
                        Total = ri.Total
                    }).ToList()
                }).ToList()
        };
    }
}
