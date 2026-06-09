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
        // that â€” including stragglers > 0 but < unit.Quantity â€” counts as out of stock.
        //   - TotalStockItems: SUM of AvailableQuantity across every row (base units).
        //   - LowStockProducts: count of (unit Ã— warehouse) rows that are sellable but at
        //     or below the unit's LowStockThreshold, matching the linked balances filter.
        //   - OutOfStockItems: count of (unit Ã— warehouse) rows that cannot satisfy one
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

}
