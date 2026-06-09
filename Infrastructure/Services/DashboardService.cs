using Application.Features.Dashboard;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // Scope order/revenue KPIs to Store-type (MS) warehouses so the tiles match the
        // Sales-by-Store and Monthly-Revenue charts.
        var storeTypeId = await _context.Lookups
            .Where(l => l.Code == WarehouseTypeCodes.BranchStore && l.ParentId != null)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var storeWarehouseIds = storeTypeId != Guid.Empty
            ? await _context.Warehouses
                .Where(w => w.WarehouseTypeId == storeTypeId)
                .Select(w => w.Id)
                .ToListAsync()
            : new List<Guid>();

        var storeOrders = _context.Orders
            .Where(o => o.WarehouseId.HasValue && storeWarehouseIds.Contains(o.WarehouseId!.Value));

        // Order statistics for current month
        var orderStats = await storeOrders
            .Where(o => o.CreatedAt >= monthStart)
            .GroupBy(o => 1)
            .Select(g => new
            {
                MonthOrders = g.Count(),
                CompletedOrders = g.Count(o => o.Status == OrderStatus.Completed),
                RefundedOrders = g.Count(o => o.Status == OrderStatus.Refunded),
                PartialRefundedOrders = g.Count(o => o.Status == OrderStatus.PartialRefunded),
                CompletedOrderTotals = g.Where(o => o.Status == OrderStatus.Completed)
                    .Sum(o => o.Total),
                PartialRefundedOrderTotals = g.Where(o => o.Status == OrderStatus.PartialRefunded)
                    .Sum(o => o.Total)
            })
            .FirstOrDefaultAsync();

        var monthRevenue = (orderStats?.CompletedOrderTotals ?? 0) + (orderStats?.PartialRefundedOrderTotals ?? 0);

        // Today's order statistics
        var todayStats = await storeOrders
            .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow)
            .GroupBy(o => 1)
            .Select(g => new
            {
                TodayOrders = g.Count(),
                TodayCompletedTotals = g.Where(o => o.Status == OrderStatus.Completed)
                    .Sum(o => o.Total),
                TodayPartialRefundedTotals = g.Where(o => o.Status == OrderStatus.PartialRefunded)
                    .Sum(o => o.Total)
            })
            .FirstOrDefaultAsync();

        var todayRevenue = (todayStats?.TodayCompletedTotals ?? 0) + (todayStats?.TodayPartialRefundedTotals ?? 0);

        // Today's net items sold (in base units) — read directly from InventoryHistory so
        // partial refunds, full refunds, and cancellations are all accounted for atomically
        // via the timestamp the movement was recorded. Sale rows have negative QuantityChange
        // and Return rows have positive QuantityChange, so negating and summing gives net
        // base units removed from stock today via order activity.
        var todayItemsSold = await _context.InventoryHistories
            .Where(h => h.PerformedAt >= today && h.PerformedAt < tomorrow
                && h.ReferenceType == "Order"
                && storeWarehouseIds.Contains(h.WarehouseId)
                && (h.ActionType == InventoryActionType.Sale || h.ActionType == InventoryActionType.Return))
            .SumAsync(h => (int?)(-h.QuantityChange)) ?? 0;

        // Product statistics
        var totalProducts = await _context.Products.CountAsync();
        var activeProducts = await _context.Products.CountAsync(p => p.IsActive);

        // Category count
        var totalCategories = await _context.Categories.CountAsync();

        // Supplier count
        var totalSuppliers = await _context.Suppliers.CountAsync();

        // Warehouse count
        var totalWarehouses = await _context.Warehouses.CountAsync();

        // Unit count
        var totalUnits = await _context.Units.CountAsync();

        // Stock balance statistics. All three tiles below link to /admin/inventory/balances.
        // Definitions are aligned with the POS/cashier panel: a unit is "sellable" only when
        // AvailableQuantity is at least unit.Quantity (one full selling unit). Anything below
        // that — including stragglers > 0 but < unit.Quantity — counts as out of stock.
        //   - TotalStockItems: SUM of AvailableQuantity across every row (base units).
        //   - LowStockProducts: count of (unit × warehouse) rows that are sellable but at
        //     or below the unit's LowStockThreshold, matching the linked balances filter.
        //   - OutOfStockItems: count of (unit × warehouse) rows that cannot satisfy one
        //     selling unit (AvailableQuantity < unit.Quantity).
        var totalStockItems = await _context.StockBalances
            .SumAsync(sb => (int?)sb.AvailableQuantity) ?? 0;

        var outOfStockItems = await _context.StockBalances
            .CountAsync(sb => sb.AvailableQuantity < sb.Unit.Quantity);

        var lowStockProducts = await _context.StockBalances
            .CountAsync(sb => sb.AvailableQuantity >= sb.Unit.Quantity
                              && sb.AvailableQuantity <= sb.Unit.LowStockThreshold);

        // User statistics
        var userStats = await _context.Users
            .GroupBy(u => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(u => u.IsActive)
            })
            .FirstOrDefaultAsync();

        return new DashboardSummaryDto
        {
            MonthOrders = orderStats?.MonthOrders ?? 0,
            CompletedOrders = orderStats?.CompletedOrders ?? 0,
            RefundedOrders = orderStats?.RefundedOrders ?? 0,
            PartialRefundedOrders = orderStats?.PartialRefundedOrders ?? 0,
            MonthRevenue = Math.Max(0, monthRevenue),
            TodayRevenue = Math.Max(0, todayRevenue),
            TodayOrders = todayStats?.TodayOrders ?? 0,
            TodayItemsSold = Math.Max(0, todayItemsSold),
            TotalProducts = totalProducts,
            ActiveProducts = activeProducts,
            LowStockProducts = lowStockProducts,
            TotalCategories = totalCategories,
            TotalSuppliers = totalSuppliers,
            TotalUnits = totalUnits,
            TotalWarehouses = totalWarehouses,
            TotalStockItems = totalStockItems,
            OutOfStockItems = outOfStockItems,
            TotalUsers = userStats?.Total ?? 0,
            ActiveUsers = userStats?.Active ?? 0
        };
    }

    public async Task<List<WarehouseProductStatsDto>> GetWarehouseProductStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        // Normalize date boundaries
        var dateFrom = fromDate?.Date;
        var dateTo = toDate?.Date.AddDays(1); // exclusive upper bound

        // Only include Store-type (MS) warehouses
        var storeTypeId = await _context.Lookups
            .Where(l => l.Code == WarehouseTypeCodes.BranchStore && l.ParentId != null)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var storeWarehouses = storeTypeId != Guid.Empty
            ? await _context.Warehouses
                .Where(w => w.WarehouseTypeId == storeTypeId && w.IsActive)
                .Select(w => new { w.Id, w.NameEn, w.NameAr })
                .ToListAsync()
            : new List<dynamic>().Select(x => new { Id = Guid.Empty, NameEn = "", NameAr = "" }).ToList();

        if (!storeWarehouses.Any())
            return new List<WarehouseProductStatsDto>();

        var storeWarehouseIds = storeWarehouses.Select(w => w.Id).ToList();

        // Base query: orders assigned to a store warehouse (all statuses to capture both partial and full refunds)
        var ordersBase = _context.Orders
            .Where(o => o.WarehouseId.HasValue
                && storeWarehouseIds.Contains(o.WarehouseId.Value));

        if (dateFrom.HasValue)
            ordersBase = ordersBase.Where(o => o.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            ordersBase = ordersBase.Where(o => o.CreatedAt < dateTo.Value);

        // Use IgnoreQueryFilters on Units so soft-deleted units still provide their base quantity
        var allUnits = _context.Units.IgnoreQueryFilters();

        // Sales and returns are read directly from InventoryHistory so partial refunds and
        // full cancellations are counted correctly regardless of how OrderItem.Quantity has
        // since been mutated. Movements are attributed to the order's store warehouse
        // (o.WarehouseId), the same dimension used by the Revenue series below, so the
        // chart's Sold / Returned / Revenue series share a consistent grouping.
        var saleData = await (
            from h in _context.InventoryHistories
            where h.ReferenceType == "Order" && h.ActionType == InventoryActionType.Sale
            join o in ordersBase on h.ReferenceId equals o.Id
            join u in allUnits on h.UnitId equals u.Id
            group new { h.QuantityChange }
                by new { WarehouseId = o.WarehouseId!.Value, ProductId = u.ProductId } into g
            select new
            {
                g.Key.WarehouseId,
                g.Key.ProductId,
                Sold = g.Sum(x => -x.QuantityChange)
            }
        ).ToListAsync();

        var returnData = await (
            from h in _context.InventoryHistories
            where h.ReferenceType == "Order" && h.ActionType == InventoryActionType.Return
            join o in ordersBase on h.ReferenceId equals o.Id
            join u in allUnits on h.UnitId equals u.Id
            group new { h.QuantityChange }
                by new { WarehouseId = o.WarehouseId!.Value, ProductId = u.ProductId } into g
            select new
            {
                g.Key.WarehouseId,
                g.Key.ProductId,
                Returned = g.Sum(x => x.QuantityChange)
            }
        ).ToListAsync();

        // Revenue per warehouse: use Completed + PartialRefunded only.
        // For PartialRefunded orders, order.Total is already net of refunds (see OrderService.PartialRefundAsync),
        // and fully-cancelled Completed orders (Status=Refunded) are excluded because their Total was not zeroed.
        var revenueByWarehouse = await (
            from o in ordersBase
            where o.Status == OrderStatus.Completed || o.Status == OrderStatus.PartialRefunded
            group o by o.WarehouseId!.Value into g
            select new
            {
                WarehouseId = g.Key,
                Revenue = g.Sum(x => x.Total)
            }
        ).ToListAsync();

        var productIds = saleData.Select(s => s.ProductId)
            .Union(returnData.Select(r => r.ProductId))
            .Distinct()
            .ToList();

        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameEn, p.NameAr })
            .ToListAsync();

        // Build result for each store warehouse (show all stores even if no sales)
        var result = storeWarehouses.Select(warehouse =>
        {
            var wId = warehouse.Id;
            var warehouseProductIds = saleData.Where(s => s.WarehouseId == wId).Select(s => s.ProductId)
                .Union(returnData.Where(r => r.WarehouseId == wId).Select(r => r.ProductId))
                .Distinct();

            var productStats = warehouseProductIds.Select(pId =>
            {
                var product = products.FirstOrDefault(p => p.Id == pId);
                var sold = saleData.FirstOrDefault(s => s.WarehouseId == wId && s.ProductId == pId)?.Sold ?? 0;
                var returned = returnData.FirstOrDefault(r => r.WarehouseId == wId && r.ProductId == pId)?.Returned ?? 0;

                return new ProductSalesStatsDto
                {
                    ProductId = pId,
                    ProductName = product?.NameEn ?? "Unknown",
                    ProductNameAr = product?.NameAr ?? "غير معروف",
                    QuantitySold = sold,
                    QuantityReturned = returned
                };
            })
            .OrderByDescending(p => p.QuantitySold)
            .Take(15)
            .ToList();

            var saleRev = revenueByWarehouse.FirstOrDefault(r => r.WarehouseId == wId)?.Revenue ?? 0m;

            return new WarehouseProductStatsDto
            {
                WarehouseId = wId,
                WarehouseName = warehouse.NameEn,
                WarehouseNameAr = warehouse.NameAr,
                Revenue = Math.Max(0, saleRev),
                Products = productStats
            };
        })
        .Where(w => w.Products.Any())
        .OrderBy(w => w.WarehouseName)
        .ToList();

        return result;
    }

    public async Task<List<WarehouseStockDto>> GetWarehouseCurrentStockAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        // Normalize date boundaries
        var dateFrom = fromDate?.Date;
        var dateTo = toDate?.Date.AddDays(1); // exclusive upper bound

        var warehouses = await _context.Warehouses
            .Where(w => w.IsActive)
            .Select(w => new
            {
                w.Id,
                w.NameEn,
                w.NameAr,
                w.WarehouseTypeId,
                TypeCode = w.WarehouseType.Code,
                TypeNameEn = w.WarehouseType.NameEn,
                TypeNameAr = w.WarehouseType.NameAr,
                BranchNameEn = w.Branch != null ? w.Branch.NameEn : null,
                BranchNameAr = w.Branch != null ? w.Branch.NameAr : null
            })
            .ToListAsync();

        var cwWarehouseIds = warehouses.Where(w => w.TypeCode == WarehouseTypeCodes.CentralWarehouse).Select(w => w.Id).ToHashSet();

        // Use IgnoreQueryFilters on Units so soft-deleted units still provide their product association
        var allUnits = _context.Units.IgnoreQueryFilters();

        var stockBalances = await (
            from sb in _context.StockBalances
            join u in allUnits on sb.UnitId equals u.Id
            join p in _context.Products.IgnoreQueryFilters() on u.ProductId equals p.Id
            group new { sb.AvailableQuantity, sb.ReservedQuantity, sb.InTransitQuantity, p.NameEn, p.NameAr }
                by new { sb.WarehouseId, ProductId = u.ProductId } into g
            select new
            {
                g.Key.WarehouseId,
                g.Key.ProductId,
                ProductNameEn = g.Max(x => x.NameEn),
                ProductNameAr = g.Max(x => x.NameAr),
                AvailableQuantity = g.Sum(x => x.AvailableQuantity),
                ReservedQuantity = g.Sum(x => x.ReservedQuantity),
                InTransitQuantity = g.Sum(x => x.InTransitQuantity)
            }
        ).ToListAsync();

        // GRN received quantities per warehouse per product (only for Central Warehouses)
        var grnQuery = _context.GoodsReceivingNotes
            .Where(grn => cwWarehouseIds.Contains(grn.WarehouseId));
        if (dateFrom.HasValue)
            grnQuery = grnQuery.Where(grn => grn.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            grnQuery = grnQuery.Where(grn => grn.CreatedAt < dateTo.Value);

        var grnReceived = await (
            from grn in grnQuery
            from line in grn.Lines
            join u in allUnits on line.UnitId equals u.Id
            group new { BaseQuantity = line.ReceivedQuantity * u.Quantity }
                by new { grn.WarehouseId, ProductId = u.ProductId } into g
            select new
            {
                g.Key.WarehouseId,
                g.Key.ProductId,
                TotalReceived = g.Sum(x => x.BaseQuantity)
            }
        ).ToListAsync();

        // Transfer IN quantities per warehouse per product
        var transferInQuery = _context.StockTransfers.AsQueryable();
        if (dateFrom.HasValue)
            transferInQuery = transferInQuery.Where(t => t.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            transferInQuery = transferInQuery.Where(t => t.CreatedAt < dateTo.Value);

        var transfersIn = await (
            from t in transferInQuery
            from line in t.Lines
            join u in allUnits on line.UnitId equals u.Id
            group new { BaseQuantity = line.Quantity * u.Quantity }
                by new { WarehouseId = t.ToWarehouseId, ProductId = u.ProductId } into g
            select new
            {
                g.Key.WarehouseId,
                g.Key.ProductId,
                TotalIn = g.Sum(x => x.BaseQuantity)
            }
        ).ToListAsync();

        // Transfer OUT quantities per warehouse per product
        var transferOutQuery = _context.StockTransfers.AsQueryable();
        if (dateFrom.HasValue)
            transferOutQuery = transferOutQuery.Where(t => t.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            transferOutQuery = transferOutQuery.Where(t => t.CreatedAt < dateTo.Value);

        var transfersOut = await (
            from t in transferOutQuery
            from line in t.Lines
            join u in allUnits on line.UnitId equals u.Id
            group new { BaseQuantity = line.Quantity * u.Quantity }
                by new { WarehouseId = t.FromWarehouseId, ProductId = u.ProductId } into g
            select new
            {
                g.Key.WarehouseId,
                g.Key.ProductId,
                TotalOut = g.Sum(x => x.BaseQuantity)
            }
        ).ToListAsync();

        // Collect all product names from stock balances, GRN lines, and transfer lines
        var allProductIds = stockBalances.Select(sb => sb.ProductId)
            .Union(grnReceived.Select(g => g.ProductId))
            .Union(transfersIn.Select(t => t.ProductId))
            .Union(transfersOut.Select(t => t.ProductId))
            .Distinct()
            .ToHashSet();

        var productNames = await _context.Products
            .Where(p => allProductIds.Contains(p.Id))
            .Select(p => new { p.Id, p.NameEn, p.NameAr })
            .ToDictionaryAsync(p => p.Id);

        // LowStockThreshold now lives on Unit; aggregate the max threshold per product
        var productThresholds = await _context.Units
            .Where(u => allProductIds.Contains(u.ProductId))
            .GroupBy(u => u.ProductId)
            .Select(g => new { ProductId = g.Key, Threshold = g.Max(u => u.LowStockThreshold) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Threshold);

        return warehouses
            .Select(w =>
            {
                // Collect all product IDs relevant to this warehouse
                var warehouseProductIds = stockBalances
                    .Where(sb => sb.WarehouseId == w.Id)
                    .Select(sb => sb.ProductId)
                    .ToHashSet();

                var isCW = cwWarehouseIds.Contains(w.Id);

                if (isCW)
                {
                    foreach (var g in grnReceived.Where(g => g.WarehouseId == w.Id))
                        warehouseProductIds.Add(g.ProductId);
                }
                foreach (var t in transfersIn.Where(t => t.WarehouseId == w.Id))
                    warehouseProductIds.Add(t.ProductId);
                foreach (var t in transfersOut.Where(t => t.WarehouseId == w.Id))
                    warehouseProductIds.Add(t.ProductId);

                var products = warehouseProductIds
                    .Select(productId =>
                    {
                        var sb = stockBalances.FirstOrDefault(s => s.WarehouseId == w.Id && s.ProductId == productId);
                        var received = isCW
                            ? (grnReceived.FirstOrDefault(r => r.WarehouseId == w.Id && r.ProductId == productId)?.TotalReceived ?? 0)
                            : 0;
                        var transferIn = transfersIn
                            .FirstOrDefault(t => t.WarehouseId == w.Id && t.ProductId == productId)?.TotalIn ?? 0;
                        var transferOut = transfersOut
                            .FirstOrDefault(t => t.WarehouseId == w.Id && t.ProductId == productId)?.TotalOut ?? 0;

                        var pName = productNames.TryGetValue(productId, out var pn) ? pn : null;

                        return new ProductStockDto
                        {
                            ProductId = productId,
                            ProductName = sb?.ProductNameEn ?? pName?.NameEn ?? string.Empty,
                            ProductNameAr = sb?.ProductNameAr ?? pName?.NameAr ?? string.Empty,
                            AvailableQuantity = sb?.AvailableQuantity ?? 0,
                            ReservedQuantity = sb?.ReservedQuantity ?? 0,
                            InTransitQuantity = sb?.InTransitQuantity ?? 0,
                            ReceivedQuantity = received,
                            TransferredInQuantity = transferIn,
                            TransferredOutQuantity = transferOut,
                            LowStockThreshold = productThresholds.TryGetValue(productId, out var th) ? th : 0
                        };
                    })
                    .OrderByDescending(p => p.AvailableQuantity)
                    .ToList();

                return new WarehouseStockDto
                {
                    WarehouseId = w.Id,
                    WarehouseName = w.NameEn,
                    WarehouseNameAr = w.NameAr,
                    WarehouseTypeId = w.WarehouseTypeId,
                    WarehouseTypeCode = w.TypeCode,
                    WarehouseTypeName = w.TypeNameEn,
                    WarehouseTypeNameAr = w.TypeNameAr,
                    BranchNameEn = w.BranchNameEn,
                    BranchNameAr = w.BranchNameAr,
                    Products = products
                };
            })
            .Where(w => w.Products.Any())
            .OrderBy(w => w.WarehouseName)
            .ToList();
    }

    public async Task<List<MonthlyRevenueDto>> GetMonthlyRevenueAsync(int months = 12)
    {
        months = Math.Clamp(months, 1, 24);
        var today = DateTime.UtcNow.Date;
        var startMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(months - 1));

        // Only include Store-type (MS) warehouses
        var storeTypeId = await _context.Lookups
            .Where(l => l.Code == WarehouseTypeCodes.BranchStore && l.ParentId != null)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();

        var storeWarehouses = storeTypeId != Guid.Empty
            ? await _context.Warehouses
                .Where(w => w.WarehouseTypeId == storeTypeId && w.IsActive)
                .Select(w => new { w.Id, w.NameEn, w.NameAr })
                .ToListAsync()
            : new List<object>().Select(x => new { Id = Guid.Empty, NameEn = "", NameAr = "" }).ToList();

        if (!storeWarehouses.Any())
            return new List<MonthlyRevenueDto>();

        var storeWarehouseIds = storeWarehouses.Select(w => w.Id).ToList();

        // Revenue from Completed + PartialRefunded orders (their Total is already net of refunds for PartialRefunded)
        var revenueData = await _context.Orders
            .Where(o => o.CreatedAt >= startMonth
                && o.WarehouseId.HasValue
                && storeWarehouseIds.Contains(o.WarehouseId!.Value)
                && (o.Status == OrderStatus.Completed || o.Status == OrderStatus.PartialRefunded))
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, WarehouseId = o.WarehouseId!.Value })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.WarehouseId,
                Revenue = g.Sum(o => o.Total)
            })
            .ToListAsync();

        // Build result for each month in the range
        var result = new List<MonthlyRevenueDto>();
        for (var i = 0; i < months; i++)
        {
            var m = startMonth.AddMonths(i);
            var dto = new MonthlyRevenueDto
            {
                Year = m.Year,
                Month = m.Month,
                Stores = storeWarehouses.Select(sw =>
                {
                    var rev = revenueData
                        .Where(r => r.Year == m.Year && r.Month == m.Month && r.WarehouseId == sw.Id)
                        .Sum(r => r.Revenue);
                    return new StoreRevenueDto
                    {
                        WarehouseId = sw.Id,
                        WarehouseName = sw.NameEn,
                        WarehouseNameAr = sw.NameAr,
                        Revenue = rev
                    };
                }).ToList()
            };
            result.Add(dto);
        }

        return result;
    }
}
