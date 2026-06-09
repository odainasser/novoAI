using Application.Common.Models;
using Application.Features.Inventory;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class InventoryHistoryService : IInventoryHistoryService
{
    private readonly ApplicationDbContext _context;

    public InventoryHistoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<InventoryHistoryDto>> GetAllAsync(
        int pageNumber, int pageSize, Guid? warehouseId = null, Guid? unitId = null,
        string? actionType = null, DateTime? fromDate = null, DateTime? toDate = null,
        string? referenceType = null, IEnumerable<Guid>? warehouseIds = null)
    {
        var query = _context.InventoryHistories
            .Include(ih => ih.Warehouse)
            .Include(ih => ih.Unit).ThenInclude(u => u.Product)
            .Include(ih => ih.Unit).ThenInclude(u => u.UnitOfMeasure)
            .Include(ih => ih.Unit).ThenInclude(u => u.UnitUnitTypes).ThenInclude(uut => uut.UnitType)
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(ih => ih.WarehouseId == warehouseId.Value);

        // Multi-warehouse filter (mirrors the orders/shifts pattern). Used by the Branch
        // Panel to scope inventory history to all warehouses owned by a branch.
        if (warehouseIds is not null)
        {
            var ids = warehouseIds.ToList();
            if (ids.Count == 0)
                return new PaginatedList<InventoryHistoryDto>(new List<InventoryHistoryDto>(), 0, pageNumber, pageSize);
            query = query.Where(ih => ids.Contains(ih.WarehouseId));
        }

        if (unitId.HasValue)
            query = query.Where(ih => ih.UnitId == unitId.Value);

        if (!string.IsNullOrWhiteSpace(actionType) && Enum.TryParse<InventoryActionType>(actionType, true, out var actionEnum))
            query = query.Where(ih => ih.ActionType == actionEnum);

        if (fromDate.HasValue)
            query = query.Where(ih => ih.PerformedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(ih => ih.PerformedAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(referenceType))
            query = query.Where(ih => ih.ReferenceType == referenceType);

        query = query.OrderByDescending(ih => ih.PerformedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<InventoryHistoryDto>(
            items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<InventoryHistoryDto?> GetByIdAsync(Guid id)
    {
        var history = await _context.InventoryHistories
            .Include(ih => ih.Warehouse)
            .Include(ih => ih.Unit).ThenInclude(u => u.Product)
            .Include(ih => ih.Unit).ThenInclude(u => u.UnitOfMeasure)
            .Include(ih => ih.Unit).ThenInclude(u => u.UnitUnitTypes).ThenInclude(uut => uut.UnitType)
            .FirstOrDefaultAsync(ih => ih.Id == id);

        return history == null ? null : MapToDto(history);
    }

    public async Task<List<StockBalanceDto>> GetStockBalancesAsync(Guid warehouseId, string? search = null)
    {
        var query = _context.StockBalances
            .Include(sb => sb.Warehouse)
            .Include(sb => sb.Unit).ThenInclude(u => u.Product)
            .Include(sb => sb.Unit).ThenInclude(u => u.UnitOfMeasure)
            .Include(sb => sb.Unit).ThenInclude(u => u.UnitUnitTypes).ThenInclude(uut => uut.UnitType)
            .Where(sb => sb.WarehouseId == warehouseId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(sb =>
                (sb.Unit.Product != null && sb.Unit.Product.NameEn.ToLower().Contains(s)) ||
                (sb.Unit.Product != null && sb.Unit.Product.NameAr.ToLower().Contains(s)) ||
                (sb.Unit.Product != null && sb.Unit.Product.Code.ToLower().Contains(s)) ||
                sb.Unit.Barcode.ToLower().Contains(s));
        }

        var balances = await query
            .OrderBy(sb => sb.Unit.Product != null ? sb.Unit.Product.NameEn : string.Empty)
            .ToListAsync();

        return balances.Select(sb => new StockBalanceDto
        {
            Id = sb.Id,
            WarehouseId = sb.WarehouseId,
            WarehouseNameEn = sb.Warehouse?.NameEn ?? string.Empty,
            WarehouseNameAr = sb.Warehouse?.NameAr ?? string.Empty,
            ProductId = sb.Unit?.ProductId ?? Guid.Empty,
            UnitId = sb.UnitId,
            UnitBarcode = sb.Unit?.Barcode ?? string.Empty,
            ProductNameEn = sb.Unit?.Product?.NameEn ?? string.Empty,
            ProductNameAr = sb.Unit?.Product?.NameAr ?? string.Empty,
            ProductCode = sb.Unit?.Product?.Code ?? string.Empty,
            UnitOfMeasureNameEn = sb.Unit?.UnitOfMeasure?.NameEn ?? string.Empty,
            UnitOfMeasureNameAr = sb.Unit?.UnitOfMeasure?.NameAr ?? string.Empty,
            UnitTypesEn = sb.Unit?.UnitUnitTypes?.Select(uut => uut.UnitType?.NameEn ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
            UnitTypesAr = sb.Unit?.UnitUnitTypes?.Select(uut => uut.UnitType?.NameAr ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
            UnitBaseQuantity = sb.Unit?.Quantity ?? 1,
            AvailableQuantity = sb.AvailableQuantity,
            ReservedQuantity = sb.ReservedQuantity,
            InTransitQuantity = sb.InTransitQuantity,
            LowStockThreshold = sb.Unit?.LowStockThreshold ?? 0,
            LastStockCheckDate = sb.LastStockCheckDate
        }).ToList();
    }

    public async Task<PaginatedList<StockBalanceDto>> GetAllStockBalancesAsync(
        int pageNumber, int pageSize, string? search = null, Guid? warehouseId = null, string? stockStatus = null, IReadOnlyList<Guid>? warehouseIds = null)
    {
        var query = _context.StockBalances
            .Include(sb => sb.Warehouse)
            .Include(sb => sb.Unit).ThenInclude(u => u.Product)
            .Include(sb => sb.Unit).ThenInclude(u => u.UnitOfMeasure)
            .Include(sb => sb.Unit).ThenInclude(u => u.UnitUnitTypes).ThenInclude(uut => uut.UnitType)
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(sb => sb.WarehouseId == warehouseId.Value);
        else if (warehouseIds is { Count: > 0 })
            query = query.Where(sb => warehouseIds.Contains(sb.WarehouseId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(sb =>
                (sb.Unit.Product != null && sb.Unit.Product.NameEn.ToLower().Contains(s)) ||
                (sb.Unit.Product != null && sb.Unit.Product.NameAr.ToLower().Contains(s)) ||
                (sb.Unit.Product != null && sb.Unit.Product.Code.ToLower().Contains(s)) ||
                sb.Unit.Barcode.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(stockStatus))
        {
            // Stock status buckets (per StockBalance row = unit × warehouse), aligned with the
            // POS/cashier definition. A row is "sellable" when AvailableQuantity is at least
            // unit.Quantity (one full selling unit). Stragglers below that threshold count as
            // out-of-stock so the cashier and admin views stay consistent.
            //   outofstock: AvailableQuantity < Unit.Quantity (cannot sell one selling unit)
            //   lowstock:   sellable AND AvailableQuantity <= Unit.LowStockThreshold
            //   instock:    sellable AND AvailableQuantity > Unit.LowStockThreshold
            if (stockStatus.Equals("outofstock", StringComparison.OrdinalIgnoreCase))
                query = query.Where(sb => sb.AvailableQuantity < sb.Unit.Quantity);
            else if (stockStatus.Equals("instock", StringComparison.OrdinalIgnoreCase))
                query = query.Where(sb => sb.AvailableQuantity >= sb.Unit.Quantity
                                          && sb.AvailableQuantity > sb.Unit.LowStockThreshold);
            else if (stockStatus.Equals("lowstock", StringComparison.OrdinalIgnoreCase))
                query = query.Where(sb => sb.AvailableQuantity >= sb.Unit.Quantity
                                          && sb.AvailableQuantity <= sb.Unit.LowStockThreshold);
        }

        query = query.OrderBy(sb => sb.Warehouse.NameEn).ThenBy(sb => sb.Unit.Product != null ? sb.Unit.Product.NameEn : string.Empty);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<StockBalanceDto>(
            items.Select(sb => new StockBalanceDto
            {
                Id = sb.Id,
                WarehouseId = sb.WarehouseId,
                WarehouseNameEn = sb.Warehouse?.NameEn ?? string.Empty,
                WarehouseNameAr = sb.Warehouse?.NameAr ?? string.Empty,
                ProductId = sb.Unit?.ProductId ?? Guid.Empty,
                UnitId = sb.UnitId,
                UnitBarcode = sb.Unit?.Barcode ?? string.Empty,
                ProductNameEn = sb.Unit?.Product?.NameEn ?? string.Empty,
                ProductNameAr = sb.Unit?.Product?.NameAr ?? string.Empty,
                ProductCode = sb.Unit?.Product?.Code ?? string.Empty,
                UnitOfMeasureNameEn = sb.Unit?.UnitOfMeasure?.NameEn ?? string.Empty,
                UnitOfMeasureNameAr = sb.Unit?.UnitOfMeasure?.NameAr ?? string.Empty,
                UnitTypesEn = sb.Unit?.UnitUnitTypes?.Select(uut => uut.UnitType?.NameEn ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
                UnitTypesAr = sb.Unit?.UnitUnitTypes?.Select(uut => uut.UnitType?.NameAr ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
                UnitBaseQuantity = sb.Unit?.Quantity ?? 1,
                AvailableQuantity = sb.AvailableQuantity,
                ReservedQuantity = sb.ReservedQuantity,
                InTransitQuantity = sb.InTransitQuantity,
                LowStockThreshold = sb.Unit?.LowStockThreshold ?? 0,
                LastStockCheckDate = sb.LastStockCheckDate
            }).ToList(), count, pageNumber, pageSize);
    }

    public async Task<int> GetTotalAvailableBySearchAsync(string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return 0;

        var s = search.ToLower();
        return await _context.StockBalances
            .Include(sb => sb.Unit).ThenInclude(u => u.Product)
            .Where(sb =>
                (sb.Unit.Product != null && sb.Unit.Product.NameEn.ToLower().Contains(s)) ||
                (sb.Unit.Product != null && sb.Unit.Product.NameAr.ToLower().Contains(s)) ||
                (sb.Unit.Product != null && sb.Unit.Product.Code.ToLower().Contains(s)) ||
                sb.Unit.Barcode.ToLower().Contains(s))
            .SumAsync(sb => sb.AvailableQuantity);
    }

    private static InventoryHistoryDto MapToDto(InventoryHistory ih) => new()
    {
        Id = ih.Id,
        WarehouseId = ih.WarehouseId,
        WarehouseNameEn = ih.Warehouse?.NameEn ?? string.Empty,
        WarehouseNameAr = ih.Warehouse?.NameAr ?? string.Empty,
        ProductId = ih.Unit?.ProductId ?? Guid.Empty,
        UnitId = ih.UnitId,
        UnitOfMeasureNameEn = ih.Unit?.UnitOfMeasure?.NameEn ?? string.Empty,
        UnitOfMeasureNameAr = ih.Unit?.UnitOfMeasure?.NameAr ?? string.Empty,
        UnitBaseQuantity = ih.Unit?.Quantity ?? 1,
        UnitBarcode = ih.Unit?.Barcode ?? string.Empty,
        ProductNameEn = ih.Unit?.Product?.NameEn ?? string.Empty,
        ProductNameAr = ih.Unit?.Product?.NameAr ?? string.Empty,
        ProductCode = ih.Unit?.Product?.Code ?? string.Empty,
        UnitTypesEn = ih.Unit?.UnitUnitTypes?.Select(uut => uut.UnitType?.NameEn ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
        UnitTypesAr = ih.Unit?.UnitUnitTypes?.Select(uut => uut.UnitType?.NameAr ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
        ActionType = ih.ActionType.ToString(),
        QuantityChange = ih.QuantityChange,
        AvailableQuantityBefore = ih.AvailableQuantityBefore,
        AvailableQuantityAfter = ih.AvailableQuantityAfter,
        ReferenceType = ih.ReferenceType,
        ReferenceId = ih.ReferenceId,
        PerformedBy = ih.PerformedBy,
        PerformedAt = ih.PerformedAt,
        Notes = ih.Notes
    };
}
