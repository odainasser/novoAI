using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class StockAdjustmentService : IStockAdjustmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaService _mediaService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INumberSequenceService _numberSequence;

    public StockAdjustmentService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService,
        IMediaService mediaService,
        IHttpContextAccessor httpContextAccessor,
        INumberSequenceService numberSequence)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
        _mediaService = mediaService;
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

    public async Task<PaginatedList<StockAdjustmentDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null, string? status = null, Guid? warehouseId = null,
        string? adjustmentType = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.StockAdjustments
            .Include(sa => sa.Warehouse)
            .Include(sa => sa.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
            .Include(sa => sa.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(sa => sa.AdjustmentNumber.Contains(search));

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<StockAdjustmentStatus>(status, true, out var statusEnum))
            query = query.Where(sa => sa.Status == statusEnum);

        if (warehouseId.HasValue)
            query = query.Where(sa => sa.WarehouseId == warehouseId.Value);

        if (!string.IsNullOrWhiteSpace(adjustmentType) && Enum.TryParse<StockAdjustmentType>(adjustmentType, true, out var typeEnum))
            query = query.Where(sa => sa.AdjustmentType == typeEnum);

        if (fromDate.HasValue)
            query = query.Where(sa => sa.RequestedDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(sa => sa.RequestedDate <= toDate.Value);

        query = query.OrderByDescending(sa => sa.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = new List<StockAdjustmentDto>();
        foreach (var item in items)
        {
            dtos.Add(await MapToDtoAsync(item));
        }

        return new PaginatedList<StockAdjustmentDto>(
            dtos, count, pageNumber, pageSize);
    }

    public async Task<StockAdjustmentDto?> GetByIdAsync(Guid id)
    {
        var adjustment = await _context.StockAdjustments
            .Include(sa => sa.Warehouse)
            .Include(sa => sa.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
            .Include(sa => sa.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure)
            .FirstOrDefaultAsync(sa => sa.Id == id);

        return adjustment == null ? null : await MapToDtoAsync(adjustment);
    }

    public async Task<StockAdjustmentDto> CreateAsync(CreateStockAdjustmentRequest request, Guid? stocktakeId = null)
    {
        Guid adjustmentId = Guid.Empty;
        string adjustmentNumberOut = string.Empty;
        Guid userIdOut = Guid.Empty;
        string? userNameOut = null;

        // When generated by approving a stocktake, the ledger entries reference the
        // stocktake (not this adjustment) and the adjustment is tagged with it.
        var historyReferenceType = stocktakeId.HasValue ? "Stocktake" : "StockAdjustment";

        async Task ApplyAsync()
        {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive)
            ?? throw new InvalidOperationException("Warehouse not found or inactive.");

        if (!Enum.TryParse<StockAdjustmentType>(request.AdjustmentType, true, out var adjType))
            throw new InvalidOperationException($"Invalid adjustment type: {request.AdjustmentType}");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new InvalidOperationException("At least one line item is required.");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var adjustmentNumber = await GenerateAdjustmentNumberAsync();

        string? stocktakeNumber = null;
        if (stocktakeId.HasValue)
            stocktakeNumber = await _context.Stocktakes
                .Where(s => s.Id == stocktakeId.Value)
                .Select(s => s.StocktakeNumber)
                .FirstOrDefaultAsync();

        var isRemoval = IsRemovalType(adjType);

        var adjustment = new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = adjustmentNumber,
            WarehouseId = request.WarehouseId,
            AdjustmentType = adjType,
            Status = StockAdjustmentStatus.Completed,
            RequestedById = userId != Guid.Empty ? userId : null,
            RequestedByName = userName,
            RequestedDate = DateTime.UtcNow,
            Explanation = request.Explanation,
            StocktakeId = stocktakeId,
            StocktakeNumber = stocktakeNumber,
            CreatedAt = DateTime.UtcNow
        };

        // Batch-load units and existing balances up front (avoids per-line N+1).
        var lineUnitIds = request.Lines.Select(l => l.UnitId).Distinct().ToList();
        var units = await _context.Units
            .Include(u => u.Product)
            .Where(u => lineUnitIds.Contains(u.Id) && u.IsActive)
            .ToListAsync();
        var unitsCache = units.ToDictionary(u => u.Id);

        var missingUnit = request.Lines.FirstOrDefault(l => !unitsCache.ContainsKey(l.UnitId));
        if (missingUnit != null)
            throw new InvalidOperationException($"Unit '{await GetUnitLabelAsync(missingUnit.UnitId)}' not found or inactive.");

        var existingBalances = await _context.StockBalances
            .Where(sb => sb.WarehouseId == request.WarehouseId && lineUnitIds.Contains(sb.UnitId))
            .ToListAsync();
        var balanceByUnit = existingBalances.ToDictionary(sb => sb.UnitId);

        foreach (var lineReq in request.Lines)
        {
            var unit = unitsCache[lineReq.UnitId];

            // Get current stock at location
            if (!balanceByUnit.TryGetValue(lineReq.UnitId, out var stockBalance))
            {
                stockBalance = new StockBalance
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = request.WarehouseId,
                    UnitId = lineReq.UnitId,
                    AvailableQuantity = 0,
                    ReservedQuantity = 0,
                    InTransitQuantity = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StockBalances.Add(stockBalance);
                balanceByUnit[lineReq.UnitId] = stockBalance;
            }

            var beforeQty = stockBalance.AvailableQuantity;

            var quantityChange = isRemoval ? -lineReq.AdjustmentQuantity * (unit.Quantity) : lineReq.AdjustmentQuantity * (unit.Quantity);
            var newQty = beforeQty + quantityChange;

            // Validate removal won't go negative
            if (isRemoval && newQty < 0)
                throw new InvalidOperationException(
                    $"Removal adjustment would make available quantity negative for unit {unit.Barcode} ({unit.Product?.NameEn}). Current: {beforeQty}, Adjusting: {lineReq.AdjustmentQuantity} x {unit.Quantity} = {lineReq.AdjustmentQuantity * unit.Quantity}.");

            stockBalance.AvailableQuantity = newQty;
            stockBalance.UpdatedAt = DateTime.UtcNow;

            adjustment.Lines.Add(new StockAdjustmentLine
            {
                Id = Guid.NewGuid(),
                StockAdjustmentId = adjustment.Id,
                UnitId = lineReq.UnitId,
                CurrentQuantity = beforeQty,
                AdjustmentQuantity = lineReq.AdjustmentQuantity,
                NewQuantity = newQty,
                Notes = lineReq.Notes,
                CreatedAt = DateTime.UtcNow
            });

            _context.InventoryHistories.Add(new InventoryHistory
            {
                Id = Guid.NewGuid(),
                WarehouseId = adjustment.WarehouseId,
                UnitId = lineReq.UnitId,
                ActionType = stocktakeId.HasValue ? InventoryActionType.Stocktake : InventoryActionType.Adjustment,
                QuantityChange = quantityChange,
                AvailableQuantityBefore = beforeQty,
                AvailableQuantityAfter = newQty,
                ReferenceType = historyReferenceType,
                ReferenceId = stocktakeId ?? adjustment.Id,
                PerformedBy = userName,
                PerformedAt = DateTime.UtcNow,
                Notes = (stocktakeId.HasValue
                            ? $"Stocktake {adjustment.StocktakeNumber} - "
                            : string.Empty)
                        + $"Adjustment {adjustment.AdjustmentNumber} - {adjustment.AdjustmentType} - {unit.Product?.NameEn ?? unit.Barcode} ({lineReq.AdjustmentQuantity} x {unit.Quantity})",
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.StockAdjustments.Add(adjustment);
        await _context.SaveChangesAsync();

        adjustmentId = adjustment.Id;
        adjustmentNumberOut = adjustment.AdjustmentNumber;
        userIdOut = userId;
        userNameOut = userName;
        }

        await ConcurrencyRetry.ExecuteWithRetryAsync(_context, ApplyAsync,
            "A concurrent update was detected on stock balances. Please retry the operation.");

        if (userIdOut != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = userIdOut,
                UserName = userNameOut,
                Action = AuditAction.Created,
                EntityName = "StockAdjustment",
                EntityId = adjustmentId.ToString(),
                Details = $"Created and applied adjustment {adjustmentNumberOut}"
            });
        }

        return (await GetByIdAsync(adjustmentId))!;
    }

    public async Task DeleteAsync(Guid id)
    {
        Guid adjustmentId = Guid.Empty;
        string adjustmentNumber = string.Empty;
        Guid userIdOut = Guid.Empty;
        string? userNameOut = null;

        async Task ApplyAsync()
        {
        var adjustment = await _context.StockAdjustments
            .Include(sa => sa.Lines)
            .FirstOrDefaultAsync(sa => sa.Id == id)
            ?? throw new KeyNotFoundException($"Stock adjustment with ID {id} not found.");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var isRemoval = IsRemovalType(adjustment.AdjustmentType);

        // Reverse stock balance changes
        foreach (var line in adjustment.Lines)
        {
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == line.UnitId);
            var baseUnits = line.AdjustmentQuantity * (unit?.Quantity ?? 1);

            var stockBalance = await _context.StockBalances
                .FirstOrDefaultAsync(sb => sb.WarehouseId == adjustment.WarehouseId && sb.UnitId == line.UnitId);

            if (stockBalance != null)
            {
                var beforeQty = stockBalance.AvailableQuantity;
                var reverseChange = isRemoval ? baseUnits : -baseUnits;
                stockBalance.AvailableQuantity += reverseChange;
                if (stockBalance.AvailableQuantity < 0)
                    throw new InvalidOperationException(
                        $"Cannot reverse adjustment {adjustment.AdjustmentNumber}: reversal would make available quantity negative for unit {line.UnitId}. Current: {beforeQty}, Reversing: {reverseChange}.");
                stockBalance.UpdatedAt = DateTime.UtcNow;

                _context.InventoryHistories.Add(new InventoryHistory
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = adjustment.WarehouseId,
                    UnitId = line.UnitId,
                    ActionType = InventoryActionType.Adjustment,
                    QuantityChange = reverseChange,
                    AvailableQuantityBefore = beforeQty,
                    AvailableQuantityAfter = stockBalance.AvailableQuantity,
                    ReferenceType = "StockAdjustment",
                    ReferenceId = adjustment.Id,
                    PerformedBy = userName,
                    PerformedAt = DateTime.UtcNow,
                    Notes = $"Reversed adjustment {adjustment.AdjustmentNumber} (deleted)",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _context.StockAdjustments.Remove(adjustment);
        await _context.SaveChangesAsync();

        adjustmentId = adjustment.Id;
        adjustmentNumber = adjustment.AdjustmentNumber;
        userIdOut = userId;
        userNameOut = userName;
        }

        await ConcurrencyRetry.ExecuteWithRetryAsync(_context, ApplyAsync,
            "A concurrent update was detected on stock balances. Please retry the operation.");

        if (userIdOut != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = userIdOut,
                UserName = userNameOut,
                Action = AuditAction.Deleted,
                EntityName = "StockAdjustment",
                EntityId = adjustmentId.ToString(),
                Details = $"Deleted adjustment {adjustmentNumber}"
            });
        }
    }

    private static bool IsRemovalType(StockAdjustmentType type) =>
        type == StockAdjustmentType.Damage ||
        type == StockAdjustmentType.Loss ||
        type == StockAdjustmentType.Theft ||
        type == StockAdjustmentType.Expiry ||
        type == StockAdjustmentType.CorrectionRemove;

    private async Task<string> GenerateAdjustmentNumberAsync()
    {
        // Atomic per-day counter — concurrency-safe.
        var prefix = $"ADJ-{DateTime.UtcNow:yyyyMMdd}-";
        var nextSeq = await _numberSequence.NextAsync($"ADJ-{DateTime.UtcNow:yyyyMMdd}");
        return $"{prefix}{nextSeq:D4}";
    }

    private async Task<StockAdjustmentDto> MapToDtoAsync(StockAdjustment sa)
    {
        var mediaList = await _mediaService.GetMediaForEntityAsync(sa.Id, EntityType.StockAdjustment, "image");
        var justificationImageUrl = mediaList.FirstOrDefault() != null ? _mediaService.GetMediaUrl(mediaList.FirstOrDefault()!) : null;

        return new StockAdjustmentDto
        {
            Id = sa.Id,
            AdjustmentNumber = sa.AdjustmentNumber,
            WarehouseId = sa.WarehouseId,
            WarehouseNameEn = sa.Warehouse?.NameEn ?? string.Empty,
            WarehouseNameAr = sa.Warehouse?.NameAr ?? string.Empty,
            AdjustmentType = sa.AdjustmentType.ToString(),
            Status = sa.Status.ToString(),
            RequestedById = sa.RequestedById,
            RequestedByName = sa.RequestedByName,
            RequestedDate = sa.RequestedDate,
            Explanation = sa.Explanation,
            JustificationImageUrl = justificationImageUrl,
            StocktakeId = sa.StocktakeId,
            StocktakeNumber = sa.StocktakeNumber,
            CreatedAt = sa.CreatedAt,
            UpdatedAt = sa.UpdatedAt,
            Lines = sa.Lines.Select(l => new StockAdjustmentLineDto
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
                CurrentQuantity = l.CurrentQuantity,
                AdjustmentQuantity = l.AdjustmentQuantity,
                NewQuantity = l.NewQuantity,
                Notes = l.Notes
            }).ToList()
        };
    }
}
