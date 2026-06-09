using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Structured physical-count workflow (Full stocktake / Cycle count). It records
/// system vs. counted quantities only and never mutates <see cref="StockBalance"/>
/// during counting. On approval it reuses <see cref="IStockAdjustmentService"/> to
/// generate one adjustment per chosen type, which applies the differences through
/// the existing balance / concurrency / ledger logic.
/// </summary>
public class StocktakeService : IStocktakeService
{
    private readonly ApplicationDbContext _context;
    private readonly IStockAdjustmentService _stockAdjustmentService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserLogService _userLogService;
    private readonly INotificationService _notificationService;
    private readonly INumberSequenceService _numberSequence;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<StocktakeService> _logger;

    public StocktakeService(
        ApplicationDbContext context,
        IStockAdjustmentService stockAdjustmentService,
        ICurrentUserService currentUserService,
        IUserLogService userLogService,
        INotificationService notificationService,
        INumberSequenceService numberSequence,
        UserManager<ApplicationUser> userManager,
        ILogger<StocktakeService> logger)
    {
        _context = context;
        _stockAdjustmentService = stockAdjustmentService;
        _currentUserService = currentUserService;
        _userLogService = userLogService;
        _notificationService = notificationService;
        _numberSequence = numberSequence;
        _userManager = userManager;
        _logger = logger;
    }

    // Adjustment types valid for a negative (counted < system) difference.
    private static readonly StockAdjustmentType[] RemovalTypes =
    {
        StockAdjustmentType.Loss,
        StockAdjustmentType.Theft,
        StockAdjustmentType.Damage,
        StockAdjustmentType.Expiry
    };

    // ===== Queries =====

    public async Task<PaginatedList<StocktakeDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null, string? type = null, string? status = null,
        Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Stocktakes
            .Include(s => s.Warehouse)
            .Include(s => s.ScopeCategory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.StocktakeNumber.Contains(search));

        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<StocktakeType>(type, true, out var typeEnum))
            query = query.Where(s => s.Type == typeEnum);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<StocktakeStatus>(status, true, out var statusEnum))
            query = query.Where(s => s.Status == statusEnum);

        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId.Value);

        if (fromDate.HasValue)
            query = query.Where(s => s.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(s => s.CreatedAt <= toDate.Value);

        query = query.OrderByDescending(s => s.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Counter summary per stocktake without loading every line entity.
        var ids = items.Select(s => s.Id).ToList();
        var lineStats = await _context.StocktakeLines
            .Where(l => ids.Contains(l.StocktakeId))
            .GroupBy(l => l.StocktakeId)
            .Select(g => new
            {
                StocktakeId = g.Key,
                Total = g.Count(),
                Counted = g.Count(l => l.CountedQuantity != null),
                Matched = g.Count(l => l.LineStatus == StocktakeLineStatus.Matched
                                       || (l.CountedQuantity != null && l.Difference == 0)),
                Flagged = g.Count(l => l.Difference != 0 && l.CountedQuantity != null)
            })
            .ToListAsync();
        var statsById = lineStats.ToDictionary(x => x.StocktakeId);

        var dtos = items.Select(s =>
        {
            var dto = MapHeaderToDto(s);
            if (statsById.TryGetValue(s.Id, out var st))
            {
                dto.TotalLines = st.Total;
                dto.CountedLines = st.Counted;
                dto.MatchedLines = st.Matched;
                dto.FlaggedLines = st.Flagged;
            }
            return dto;
        }).ToList();

        return new PaginatedList<StocktakeDto>(dtos, count, pageNumber, pageSize);
    }

    public async Task<StocktakeDto?> GetByIdAsync(Guid id)
    {
        var stocktake = await LoadWithLinesAsync(id);
        return stocktake == null ? null : MapToDto(stocktake);
    }

    // ===== Lifecycle =====

    public async Task<StocktakeDto> CreateAsync(CreateStocktakeRequest request)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive)
            ?? throw new InvalidOperationException("Warehouse not found or inactive.");

        if (!Enum.TryParse<StocktakeType>(request.Type, true, out var type))
            throw new InvalidOperationException($"Invalid stocktake type: {request.Type}");

        if (!Enum.TryParse<StocktakeScopeType>(request.ScopeType, true, out var scopeType))
            throw new InvalidOperationException($"Invalid scope type: {request.ScopeType}");

        if (type == StocktakeType.Full)
            scopeType = StocktakeScopeType.All;

        if (scopeType == StocktakeScopeType.Category)
        {
            if (request.ScopeCategoryId == null)
                throw new InvalidOperationException("A category is required for a category cycle count.");
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.ScopeCategoryId.Value);
            if (!categoryExists)
                throw new InvalidOperationException("Selected category not found.");
        }

        if (scopeType == StocktakeScopeType.Products && (request.UnitIds == null || request.UnitIds.Count == 0))
            throw new InvalidOperationException("At least one product/unit is required for a product cycle count.");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();
        var number = await GenerateStocktakeNumberAsync();

        var stocktake = new Stocktake
        {
            Id = Guid.NewGuid(),
            StocktakeNumber = number,
            WarehouseId = request.WarehouseId,
            Type = type,
            ScopeType = scopeType,
            ScopeCategoryId = scopeType == StocktakeScopeType.Category ? request.ScopeCategoryId : null,
            Status = StocktakeStatus.Draft,
            CreatedById = userId != Guid.Empty ? userId : null,
            CreatedByName = userName,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        // Snapshot the in-scope units into lines now so the selection (especially a
        // product selection) is captured; Start refreshes the system quantities.
        var snapshots = await ResolveScopeSnapshotsAsync(request.WarehouseId, scopeType, request.ScopeCategoryId, request.UnitIds);
        foreach (var snap in snapshots)
        {
            stocktake.Lines.Add(new StocktakeLine
            {
                Id = Guid.NewGuid(),
                StocktakeId = stocktake.Id,
                UnitId = snap.UnitId,
                SystemQuantity = snap.SystemUnitCount,
                CountedQuantity = null,
                Difference = 0,
                LineStatus = StocktakeLineStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.Stocktakes.Add(stocktake);
        await _context.SaveChangesAsync();

        await LogAsync(userId, userName, AuditAction.Created, stocktake.Id,
            $"Created {type} stocktake {number} ({stocktake.Lines.Count} lines)");

        return (await GetByIdAsync(stocktake.Id))!;
    }

    public async Task<StocktakeDto> StartAsync(Guid id)
    {
        var stocktake = await LoadWithLinesAsync(id)
            ?? throw new KeyNotFoundException($"Stocktake {id} not found.");

        if (stocktake.Status != StocktakeStatus.Draft)
            throw new InvalidOperationException("Only a draft stocktake can be started.");

        // Refresh the system-quantity snapshot at the moment counting begins.
        var unitIds = stocktake.Lines.Select(l => l.UnitId).Distinct().ToList();
        var units = await _context.Units.Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Quantity);
        var balances = await _context.StockBalances
            .Where(b => b.WarehouseId == stocktake.WarehouseId && unitIds.Contains(b.UnitId))
            .ToDictionaryAsync(b => b.UnitId, b => b.AvailableQuantity);

        foreach (var line in stocktake.Lines)
        {
            var baseQty = units.TryGetValue(line.UnitId, out var q) ? Math.Max(1, q) : 1;
            var available = balances.TryGetValue(line.UnitId, out var a) ? a : 0;
            line.SystemQuantity = available / baseQty;
            line.CountedQuantity = null;
            line.Difference = 0;
            line.LineStatus = StocktakeLineStatus.Pending;
            line.UpdatedAt = DateTime.UtcNow;
        }

        stocktake.Status = StocktakeStatus.InProgress;
        stocktake.StartedAt = DateTime.UtcNow;
        stocktake.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();
        await LogAsync(userId, userName, AuditAction.Updated, stocktake.Id,
            $"Started counting for stocktake {stocktake.StocktakeNumber}");

        return MapToDto(stocktake);
    }

    public async Task<StocktakeDto> SaveCountsAsync(Guid id, SaveStocktakeCountsRequest request)
    {
        var stocktake = await LoadWithLinesAsync(id)
            ?? throw new KeyNotFoundException($"Stocktake {id} not found.");

        if (stocktake.Status != StocktakeStatus.InProgress)
            throw new InvalidOperationException("Counts can only be saved while a stocktake is in progress.");

        var linesById = stocktake.Lines.ToDictionary(l => l.Id);
        foreach (var entry in request.Lines)
        {
            if (!linesById.TryGetValue(entry.LineId, out var line))
                continue; // ignore lines that don't belong to this stocktake

            if (entry.CountedQuantity < 0)
                throw new InvalidOperationException("Counted quantity cannot be negative.");

            line.CountedQuantity = entry.CountedQuantity;
            line.Difference = entry.CountedQuantity - line.SystemQuantity;
            line.LineStatus = StocktakeLineStatus.Counted;
            line.Notes = entry.Notes;
            line.UpdatedAt = DateTime.UtcNow;
        }

        // Counting NEVER touches StockBalance — only the count rows are persisted.
        await _context.SaveChangesAsync();
        return MapToDto(stocktake);
    }

    public async Task<StocktakeDto> CompleteAsync(Guid id)
    {
        var stocktake = await LoadWithLinesAsync(id)
            ?? throw new KeyNotFoundException($"Stocktake {id} not found.");

        if (stocktake.Status != StocktakeStatus.InProgress)
            throw new InvalidOperationException("Only an in-progress stocktake can be completed.");

        foreach (var line in stocktake.Lines)
        {
            // Uncounted lines are treated as "no change found".
            if (line.CountedQuantity == null)
            {
                line.CountedQuantity = line.SystemQuantity;
                line.Difference = 0;
            }
            else
            {
                line.Difference = line.CountedQuantity.Value - line.SystemQuantity;
            }

            line.LineStatus = line.Difference == 0 ? StocktakeLineStatus.Matched : StocktakeLineStatus.Flagged;
            line.UpdatedAt = DateTime.UtcNow;
        }

        stocktake.Status = StocktakeStatus.Completed;
        stocktake.CompletedAt = DateTime.UtcNow;
        stocktake.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();
        var flagged = stocktake.Lines.Count(l => l.Difference != 0);
        await LogAsync(userId, userName, AuditAction.Updated, stocktake.Id,
            $"Completed stocktake {stocktake.StocktakeNumber} ({flagged} discrepancies)");

        if (flagged > 0)
            await NotifyReviewersAsync(stocktake, flagged);

        return MapToDto(stocktake);
    }

    public async Task<StocktakeDto> ApproveAsync(Guid id, ApproveStocktakeRequest request)
    {
        // 1) Read & validate without mutating tracked state (the adjustment service
        //    runs its own transaction/retry, so we must not have pending changes).
        var planning = await _context.Stocktakes
            .Include(s => s.Lines).ThenInclude(l => l.Unit)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Stocktake {id} not found.");

        if (planning.Status != StocktakeStatus.Completed)
            throw new InvalidOperationException("Only a completed stocktake can be approved.");

        var requestedTypes = request.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.AdjustmentType))
            .ToDictionary(l => l.LineId, l => l.AdjustmentType!);

        // Resolve the adjustment type for every difference line that hasn't already
        // produced an adjustment (idempotent against a partial earlier approval).
        var plan = new List<(Guid LineId, Guid UnitId, StockAdjustmentType Type, int Quantity, string? Notes)>();
        foreach (var line in planning.Lines.Where(l => l.Difference != 0 && l.GeneratedAdjustmentId == null))
        {
            // The manager must explicitly choose the adjustment type for every
            // difference line — nothing is auto-filled.
            if (!requestedTypes.TryGetValue(line.Id, out var raw)
                || !Enum.TryParse(raw, true, out StockAdjustmentType type))
                throw new InvalidOperationException(
                    "Each difference line must be assigned an adjustment type before approval.");

            // The chosen type must match the direction of the difference.
            if (line.Difference < 0 && !RemovalTypes.Contains(type))
                throw new InvalidOperationException(
                    "A shortage line must be assigned one of Loss, Theft, Damage or Expiry.");
            if (line.Difference > 0 && type != StockAdjustmentType.CorrectionAdd)
                throw new InvalidOperationException(
                    "An overage line must be assigned CorrectionAdd.");

            plan.Add((line.Id, line.UnitId, type, Math.Abs(line.Difference), line.Notes));
        }

        // 2) Generate one adjustment per chosen type, reusing the adjustment service.
        var lineResult = new Dictionary<Guid, (StockAdjustmentType Type, Guid AdjId, string AdjNumber)>();
        foreach (var group in plan.GroupBy(p => p.Type))
        {
            var adjustmentRequest = new CreateStockAdjustmentRequest
            {
                WarehouseId = planning.WarehouseId,
                AdjustmentType = group.Key.ToString(),
                Explanation = $"Generated from stocktake {planning.StocktakeNumber}",
                Lines = group.Select(p => new CreateStockAdjustmentLineRequest
                {
                    UnitId = p.UnitId,
                    AdjustmentQuantity = p.Quantity,
                    Notes = p.Notes
                }).ToList()
            };

            var created = await _stockAdjustmentService.CreateAsync(adjustmentRequest, stocktakeId: planning.Id);
            foreach (var p in group)
                lineResult[p.LineId] = (group.Key, created.Id, created.AdjustmentNumber);
        }

        // 3) Re-load tracked and persist the approval outcome onto the stocktake.
        var stocktake = await LoadWithLinesAsync(id)
            ?? throw new KeyNotFoundException($"Stocktake {id} not found.");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        foreach (var line in stocktake.Lines)
        {
            if (lineResult.TryGetValue(line.Id, out var res))
            {
                line.AdjustmentType = res.Type;
                line.GeneratedAdjustmentId = res.AdjId;
                line.GeneratedAdjustmentNumber = res.AdjNumber;
            }
            line.LineStatus = StocktakeLineStatus.Approved;
            line.UpdatedAt = DateTime.UtcNow;
        }

        stocktake.Status = StocktakeStatus.Approved;
        stocktake.ApprovedById = userId != Guid.Empty ? userId : null;
        stocktake.ApprovedByName = userName;
        stocktake.ApprovedAt = DateTime.UtcNow;
        stocktake.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAsync(userId, userName, AuditAction.ApprovedRequest, stocktake.Id,
            $"Approved stocktake {stocktake.StocktakeNumber}; generated {lineResult.Values.Select(v => v.AdjId).Distinct().Count()} adjustment(s)");

        return MapToDto(stocktake);
    }

    public async Task<StocktakeDto> CancelAsync(Guid id)
    {
        var stocktake = await LoadWithLinesAsync(id)
            ?? throw new KeyNotFoundException($"Stocktake {id} not found.");

        if (stocktake.Status is StocktakeStatus.Approved or StocktakeStatus.Cancelled)
            throw new InvalidOperationException("An approved or cancelled stocktake cannot be cancelled.");

        stocktake.Status = StocktakeStatus.Cancelled;
        stocktake.UpdatedAt = DateTime.UtcNow;
        // StockBalance is never touched on cancel.
        await _context.SaveChangesAsync();

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();
        await LogAsync(userId, userName, AuditAction.Updated, stocktake.Id,
            $"Cancelled stocktake {stocktake.StocktakeNumber}");

        return MapToDto(stocktake);
    }

    // ===== Helpers =====

    private async Task<Stocktake?> LoadWithLinesAsync(Guid id) =>
        await _context.Stocktakes
            .Include(s => s.Warehouse)
            .Include(s => s.ScopeCategory)
            .Include(s => s.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
            .Include(s => s.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure)
            .FirstOrDefaultAsync(s => s.Id == id);

    private async Task<string> GenerateStocktakeNumberAsync()
    {
        // Global atomic counter — STK-00001, STK-00002, …
        var next = await _numberSequence.NextAsync("STK");
        return $"STK-{next:D5}";
    }

    /// <summary>
    /// Resolves the in-scope units and snapshots each unit's current available
    /// quantity (expressed in unit-count) from StockBalance.
    /// </summary>
    private async Task<List<(Guid UnitId, int SystemUnitCount)>> ResolveScopeSnapshotsAsync(
        Guid warehouseId, StocktakeScopeType scopeType, Guid? categoryId, List<Guid>? unitIds)
    {
        List<Guid> targetUnitIds;

        switch (scopeType)
        {
            case StocktakeScopeType.All:
                // Every unit currently tracked in this warehouse.
                targetUnitIds = await _context.StockBalances
                    .Where(b => b.WarehouseId == warehouseId)
                    .Select(b => b.UnitId)
                    .Distinct()
                    .ToListAsync();
                break;

            case StocktakeScopeType.Category:
                targetUnitIds = await _context.Units
                    .Where(u => u.IsActive && u.Product != null && u.Product.CategoryId == categoryId)
                    .Select(u => u.Id)
                    .ToListAsync();
                break;

            case StocktakeScopeType.Products:
                targetUnitIds = (unitIds ?? new List<Guid>()).Distinct().ToList();
                break;

            default:
                targetUnitIds = new List<Guid>();
                break;
        }

        if (targetUnitIds.Count == 0)
            return new List<(Guid, int)>();

        var unitBaseQty = await _context.Units
            .Where(u => targetUnitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Quantity);

        var balances = await _context.StockBalances
            .Where(b => b.WarehouseId == warehouseId && targetUnitIds.Contains(b.UnitId))
            .ToDictionaryAsync(b => b.UnitId, b => b.AvailableQuantity);

        var result = new List<(Guid, int)>();
        foreach (var unitId in targetUnitIds)
        {
            if (!unitBaseQty.TryGetValue(unitId, out var q))
                continue; // unit no longer exists
            var baseQty = Math.Max(1, q);
            var available = balances.TryGetValue(unitId, out var a) ? a : 0;
            result.Add((unitId, available / baseQty));
        }

        return result;
    }

    private async Task NotifyReviewersAsync(Stocktake stocktake, int discrepancies)
    {
        try
        {
            var warehouse = await _context.Warehouses.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == stocktake.WarehouseId);

            var recipients = new HashSet<Guid>();

            var admins = await _userManager.GetUsersInRoleAsync(Roles.Administrator);
            foreach (var a in admins)
                if (a.IsActive && !a.IsDeleted) recipients.Add(a.Id);

            if (warehouse?.BranchId != null)
            {
                var branchUserIds = await (from ub in _context.UserBranches
                                           join u in _context.Set<ApplicationUser>() on ub.UserId equals u.Id
                                           where ub.BranchId == warehouse.BranchId.Value && u.IsActive && !u.IsDeleted
                                           select u.Id).ToListAsync();
                foreach (var uid in branchUserIds) recipients.Add(uid);
            }

            if (recipients.Count == 0) return;

            var whEn = warehouse?.NameEn ?? string.Empty;
            var whAr = warehouse?.NameAr ?? string.Empty;

            await _notificationService.SendBulkAsync(
                recipients,
                NotificationType.StocktakeReview,
                titleEn: $"Stocktake {stocktake.StocktakeNumber} awaiting review",
                titleAr: $"الجرد {stocktake.StocktakeNumber} بانتظار المراجعة",
                bodyEn: $"{discrepancies} discrepancy(ies) found at {whEn}. Review and approve to apply adjustments.",
                bodyAr: $"تم العثور على {discrepancies} فروقات في {whAr}. راجع واعتمد لتطبيق التسويات.",
                link: $"/admin/inventory/stocktake?id={stocktake.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send stocktake-review notification for {StocktakeId}", stocktake.Id);
        }
    }

    private async Task LogAsync(Guid userId, string? userName, AuditAction action, Guid stocktakeId, string details)
    {
        if (userId == Guid.Empty) return;
        await _userLogService.LogAsync(new CreateUserLogRequest
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = "Stocktake",
            EntityId = stocktakeId.ToString(),
            Details = details
        });
    }

    private static StocktakeDto MapHeaderToDto(Stocktake s) => new()
    {
        Id = s.Id,
        StocktakeNumber = s.StocktakeNumber,
        WarehouseId = s.WarehouseId,
        WarehouseNameEn = s.Warehouse?.NameEn ?? string.Empty,
        WarehouseNameAr = s.Warehouse?.NameAr ?? string.Empty,
        Type = s.Type.ToString(),
        ScopeType = s.ScopeType.ToString(),
        ScopeCategoryId = s.ScopeCategoryId,
        ScopeCategoryNameEn = s.ScopeCategory?.NameEn,
        ScopeCategoryNameAr = s.ScopeCategory?.NameAr,
        Status = s.Status.ToString(),
        CreatedById = s.CreatedById,
        CreatedByName = s.CreatedByName,
        CreatedAt = s.CreatedAt,
        StartedAt = s.StartedAt,
        CompletedAt = s.CompletedAt,
        ApprovedById = s.ApprovedById,
        ApprovedByName = s.ApprovedByName,
        ApprovedAt = s.ApprovedAt,
        Notes = s.Notes
    };

    private static StocktakeDto MapToDto(Stocktake s)
    {
        var dto = MapHeaderToDto(s);
        dto.TotalLines = s.Lines.Count;
        dto.CountedLines = s.Lines.Count(l => l.CountedQuantity != null);
        dto.MatchedLines = s.Lines.Count(l => l.CountedQuantity != null && l.Difference == 0);
        dto.FlaggedLines = s.Lines.Count(l => l.CountedQuantity != null && l.Difference != 0);
        dto.Lines = s.Lines
            .OrderByDescending(l => l.Difference != 0)
            .ThenBy(l => l.Unit?.Product?.NameEn)
            .Select(l => new StocktakeLineDto
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
                SystemQuantity = l.SystemQuantity,
                CountedQuantity = l.CountedQuantity,
                Difference = l.Difference,
                LineStatus = l.LineStatus.ToString(),
                AdjustmentType = l.AdjustmentType?.ToString(),
                GeneratedAdjustmentId = l.GeneratedAdjustmentId,
                GeneratedAdjustmentNumber = l.GeneratedAdjustmentNumber,
                Notes = l.Notes
            }).ToList();
        return dto;
    }
}
