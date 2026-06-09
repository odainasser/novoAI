using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class GoodsReceivingService : IGoodsReceivingService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INumberSequenceService _numberSequence;

    public GoodsReceivingService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        INumberSequenceService numberSequence)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _numberSequence = numberSequence;
    }

    private bool IsArabicCulture()
    {
        var acceptLanguage = _httpContextAccessor.HttpContext?.Request.Headers["Accept-Language"].ToString();
        return acceptLanguage?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<string> GetUnitLabelAsync(Guid unitId)
    {
        var isArabic = IsArabicCulture();
        var unitForName = await _context.Units
            .Include(u => u.Product)
            .FirstOrDefaultAsync(u => u.Id == unitId);
        if (unitForName != null)
        {
            return isArabic
                ? (unitForName.Product?.NameAr ?? unitForName.Product?.NameEn ?? unitForName.Barcode)
                : (unitForName.Product?.NameEn ?? unitForName.Product?.NameAr ?? unitForName.Barcode);
        }

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == unitId);
        if (product != null)
            return isArabic ? (product.NameAr ?? product.NameEn) : (product.NameEn ?? product.NameAr);

        return unitId.ToString();
    }

    public async Task<PaginatedList<GoodsReceivingNoteDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null, Guid? supplierId = null,
        Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.GoodsReceivingNotes
            .Include(g => g.Supplier)
            .Include(g => g.Warehouse)
            .Include(g => g.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
            .Include(g => g.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure)
            .Include(g => g.Lines).ThenInclude(l => l.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.GRNNumber.Contains(search) ||
                (g.Supplier != null && (g.Supplier.NameEn.Contains(search) || g.Supplier.NameAr.Contains(search))));

        if (supplierId.HasValue)
            query = query.Where(g => g.SupplierId == supplierId.Value);

        if (warehouseId.HasValue)
            query = query.Where(g => g.WarehouseId == warehouseId.Value);

        if (fromDate.HasValue)
            query = query.Where(g => g.ReceivedDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(g => g.ReceivedDate <= toDate.Value);

        query = query.OrderByDescending(g => g.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<GoodsReceivingNoteDto>(
            items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<GoodsReceivingNoteDto?> GetByIdAsync(Guid id)
    {
        var grn = await _context.GoodsReceivingNotes
            .Include(g => g.Supplier)
            .Include(g => g.Warehouse)
            .Include(g => g.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
            .Include(g => g.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure)
            .Include(g => g.Lines).ThenInclude(l => l.Supplier)
            .FirstOrDefaultAsync(g => g.Id == id);

        return grn == null ? null : MapToDto(grn);
    }

    public async Task<GoodsReceivingNoteDto> CreateAsync(CreateGoodsReceivingNoteRequest request)
    {
        Guid grnId = Guid.Empty;
        string grnNumberOut = string.Empty;
        Guid userIdOut = Guid.Empty;
        string? userNameOut = null;

        async Task ApplyAsync()
        {
        // Auto-resolve Central Warehouse (all GRNs go to Central)
        var warehouse = await _context.Warehouses
            .Include(w => w.WarehouseType)
            .FirstOrDefaultAsync(w => w.WarehouseType != null && w.WarehouseType.Code == WarehouseTypeCodes.CentralWarehouse && w.IsActive)
            ?? throw new InvalidOperationException("Central Warehouse not found or inactive.");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        // Generate GRN number
        var grnNumber = await GenerateGRNNumberAsync();

        var grn = new GoodsReceivingNote
        {
            Id = Guid.NewGuid(),
            GRNNumber = grnNumber,
            WarehouseId = warehouse.Id,
            PurchaseOrderReference = request.PurchaseOrderReference,
            ReceivedBy = userName,
            ReceivedDate = request.ReceivedDate ?? DateTime.UtcNow,
            RequestedById = userId != Guid.Empty ? userId : null,
            RequestedByName = userName,
            PurchaseRequestId = request.PurchaseRequestId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        // Batch-load every referenced unit and supplier up front (avoids the
        // previous per-line N+1 queries).
        var lineUnitIds = request.Lines.Select(l => l.UnitId).Distinct().ToList();
        var units = await _context.Units
            .Include(u => u.Product)
            .Where(u => lineUnitIds.Contains(u.Id) && u.IsActive)
            .ToListAsync();
        var unitsCache = units.ToDictionary(u => u.Id);

        var missingUnit = request.Lines.FirstOrDefault(l => !unitsCache.ContainsKey(l.UnitId));
        if (missingUnit != null)
            throw new InvalidOperationException($"Unit '{await GetUnitLabelAsync(missingUnit.UnitId)}' not found or inactive.");

        var lineSupplierIds = request.Lines.Select(l => l.SupplierId).Distinct().ToList();
        var validSupplierIds = (await _context.Suppliers
            .Where(s => lineSupplierIds.Contains(s.Id) && s.IsActive)
            .Select(s => s.Id)
            .ToListAsync()).ToHashSet();
        var missingSupplier = request.Lines.FirstOrDefault(l => !validSupplierIds.Contains(l.SupplierId));
        if (missingSupplier != null)
            throw new InvalidOperationException($"Supplier {missingSupplier.SupplierId} not found or inactive.");

        foreach (var lineReq in request.Lines)
        {
            grn.Lines.Add(new GoodsReceivingNoteLine
            {
                Id = Guid.NewGuid(),
                GoodsReceivingNoteId = grn.Id,
                UnitId = lineReq.UnitId,
                SupplierId = lineReq.SupplierId,
                Cost = lineReq.Cost,
                ReceivedQuantity = lineReq.ReceivedQuantity,
                Notes = lineReq.Notes,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Calculate total base items (ReceivedQuantity × Unit base quantity)
        grn.TotalItems = grn.Lines.Sum(line => line.ReceivedQuantity * unitsCache[line.UnitId].Quantity);

        _context.GoodsReceivingNotes.Add(grn);

        // Update stock balances and create inventory history in the same unit of
        // work as the GRN itself, then persist everything with a single
        // SaveChanges so the note and its stock effect commit atomically.
        var existingBalances = await _context.StockBalances
            .Where(sb => sb.WarehouseId == grn.WarehouseId && lineUnitIds.Contains(sb.UnitId))
            .ToListAsync();
        var balanceByUnit = existingBalances.ToDictionary(sb => sb.UnitId);

        foreach (var line in grn.Lines)
        {
            var unit = unitsCache[line.UnitId];
            var baseUnitsReceived = line.ReceivedQuantity * unit.Quantity;

            if (!balanceByUnit.TryGetValue(line.UnitId, out var stockBalance))
            {
                stockBalance = new StockBalance
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = grn.WarehouseId,
                    UnitId = line.UnitId,
                    AvailableQuantity = 0,
                    ReservedQuantity = 0,
                    InTransitQuantity = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StockBalances.Add(stockBalance);
                balanceByUnit[line.UnitId] = stockBalance;
            }

            var beforeQty = stockBalance.AvailableQuantity;
            stockBalance.AvailableQuantity += baseUnitsReceived;
            stockBalance.UpdatedAt = DateTime.UtcNow;

            var unitName = unit.Product?.NameEn ?? unit.Barcode;

            _context.InventoryHistories.Add(new InventoryHistory
            {
                Id = Guid.NewGuid(),
                WarehouseId = grn.WarehouseId,
                UnitId = line.UnitId,
                ActionType = InventoryActionType.GoodsReceiving,
                QuantityChange = baseUnitsReceived,
                AvailableQuantityBefore = beforeQty,
                AvailableQuantityAfter = stockBalance.AvailableQuantity,
                ReferenceType = "GoodsReceivingNote",
                ReferenceId = grn.Id,
                PerformedBy = userName,
                PerformedAt = DateTime.UtcNow,
                Notes = $"GRN {grn.GRNNumber} - {unitName} ({line.ReceivedQuantity} x {unit.Quantity})",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        grnId = grn.Id;
        grnNumberOut = grn.GRNNumber;
        userIdOut = userId;
        userNameOut = userName;
        }

        await ConcurrencyRetry.ExecuteWithRetryAsync(_context, ApplyAsync,
            "A concurrent update was detected on stock balances. Please retry the operation.");

        // Reload with navigation properties
        var result = await GetByIdAsync(grnId);

        if (userIdOut != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = userIdOut,
                UserName = userNameOut,
                Action = AuditAction.Created,
                EntityName = "GoodsReceivingNote",
                EntityId = grnId.ToString(),
                Details = $"Created GRN {grnNumberOut}"
            });
        }

        return result!;
    }

    public async Task DeleteAsync(Guid id)
    {
        await Task.CompletedTask;
        throw new InvalidOperationException("Approved GRN cannot be deleted.");
    }

    private async Task<string> GenerateGRNNumberAsync()
    {
        // Atomic per-day counter — concurrency-safe, replacing the previous
        // "select max + 1" scan that could collide under simultaneous receipts.
        var prefix = $"GRN-{DateTime.UtcNow:yyyyMMdd}-";
        var nextSeq = await _numberSequence.NextAsync($"GRN-{DateTime.UtcNow:yyyyMMdd}");
        return $"{prefix}{nextSeq:D4}";
    }

    private static GoodsReceivingNoteDto MapToDto(GoodsReceivingNote g) => new()
    {
        Id = g.Id,
        GRNNumber = g.GRNNumber,
        WarehouseId = g.WarehouseId,
        WarehouseNameEn = g.Warehouse?.NameEn ?? string.Empty,
        WarehouseNameAr = g.Warehouse?.NameAr ?? string.Empty,
        PurchaseOrderReference = g.PurchaseOrderReference,
        ReceivedBy = g.ReceivedBy,
        RequestedById = g.RequestedById,
        RequestedBy = g.RequestedByName ?? g.CreatedBy,
        ReceivedDate = g.ReceivedDate,
        TotalItems = g.TotalItems,
        Notes = g.Notes,
        AttachmentPath = g.AttachmentPath,
        CreatedAt = g.CreatedAt,
        UpdatedAt = g.UpdatedAt,
        Lines = g.Lines.Select(l => new GoodsReceivingNoteLineDto
        {
            Id = l.Id,
            UnitId = l.UnitId,
            UnitBarcode = l.Unit?.Barcode ?? string.Empty,
            ProductNameEn = l.Unit?.Product?.NameEn ?? string.Empty,
            ProductNameAr = l.Unit?.Product?.NameAr ?? string.Empty,
            ProductCode = l.Unit?.Product?.Code ?? string.Empty,
            UnitOfMeasureNameEn = l.Unit?.UnitOfMeasure?.NameEn ?? string.Empty,
            UnitOfMeasureNameAr = l.Unit?.UnitOfMeasure?.NameAr ?? string.Empty,
            UnitBaseQuantity = l.Unit?.Quantity ?? 1,
            SupplierId = l.SupplierId,
            SupplierNameEn = l.Supplier?.NameEn ?? string.Empty,
            SupplierNameAr = l.Supplier?.NameAr ?? string.Empty,
            Cost = l.Cost,
            ReceivedQuantity = l.ReceivedQuantity,
            Notes = l.Notes
        }).ToList()
    };
}
