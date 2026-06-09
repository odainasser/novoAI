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

public class StockTransferService : IStockTransferService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INumberSequenceService _numberSequence;

    public StockTransferService(
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

    public async Task<PaginatedList<StockTransferDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null,
        Guid? warehouseId = null, string? transferType = null,
        DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.StockTransfers
            .Include(t => t.FromWarehouse).ThenInclude(w => w.WarehouseType)
            .Include(t => t.ToWarehouse).ThenInclude(w => w.WarehouseType)
            .Include(t => t.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
            .Include(t => t.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.TransferNumber.Contains(search) ||
                (t.Notes != null && t.Notes.Contains(search)));

        if (warehouseId.HasValue)
        {
            if (transferType == "ToCentral")
                query = query.Where(t => t.FromWarehouseId == warehouseId.Value);
            else if (transferType == "FromCentral")
                query = query.Where(t => t.ToWarehouseId == warehouseId.Value);
            else
                query = query.Where(t => t.FromWarehouseId == warehouseId.Value || t.ToWarehouseId == warehouseId.Value);
        }
        else if (!string.IsNullOrEmpty(transferType))
        {
            if (transferType == "ToCentral")
                query = query.Where(t => t.ToWarehouse.WarehouseType != null && t.ToWarehouse.WarehouseType.Code == WarehouseTypeCodes.CentralWarehouse);
            else if (transferType == "FromCentral")
                query = query.Where(t => t.FromWarehouse.WarehouseType != null && t.FromWarehouse.WarehouseType.Code == WarehouseTypeCodes.CentralWarehouse);
        }

        if (fromDate.HasValue)
            query = query.Where(t => t.RequestedDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.RequestedDate <= toDate.Value);

        query = query.OrderByDescending(t => t.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<StockTransferDto>(
            items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<StockTransferDto?> GetByIdAsync(Guid id)
    {
        var transfer = await _context.StockTransfers
            .Include(t => t.FromWarehouse).ThenInclude(w => w.WarehouseType)
            .Include(t => t.ToWarehouse).ThenInclude(w => w.WarehouseType)
            .Include(t => t.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
            .Include(t => t.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure)
            .FirstOrDefaultAsync(t => t.Id == id);

        return transfer == null ? null : MapToDto(transfer);
    }

    public async Task<StockTransferDto> CreateAsync(CreateStockTransferRequest request)
    {
        Guid transferIdOut = Guid.Empty;
        string transferNumberOut = string.Empty;
        Guid userIdOut = Guid.Empty;
        string? userNameOut = null;
        string fromWarehouseNameOut = string.Empty;
        string toWarehouseNameOut = string.Empty;

        async Task ApplyAsync()
        {
        // Resolve central warehouse
        var centralWarehouse = await _context.Warehouses
            .Include(w => w.WarehouseType)
            .FirstOrDefaultAsync(w => w.WarehouseType != null && w.WarehouseType.Code == WarehouseTypeCodes.CentralWarehouse && w.IsActive)
            ?? throw new InvalidOperationException("Central Warehouse not found or inactive.");

        // Resolve from/to based on transfer type
        Guid fromWarehouseId, toWarehouseId;
        if (request.TransferType == "ToCentral")
        {
            fromWarehouseId = request.WarehouseId;
            toWarehouseId = centralWarehouse.Id;
        }
        else if (request.TransferType == "FromCentral")
        {
            fromWarehouseId = centralWarehouse.Id;
            toWarehouseId = request.WarehouseId;
        }
        else
        {
            throw new InvalidOperationException("Invalid transfer type. Must be 'ToCentral' or 'FromCentral'.");
        }

        if (fromWarehouseId == toWarehouseId)
            throw new InvalidOperationException("Source and destination warehouses must be different.");

        var fromWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == fromWarehouseId && w.IsActive)
            ?? throw new InvalidOperationException("Source warehouse not found or inactive.");

        var toWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == toWarehouseId && w.IsActive)
            ?? throw new InvalidOperationException("Destination warehouse not found or inactive.");

        if (request.Lines == null || request.Lines.Count == 0)
            throw new InvalidOperationException("At least one transfer line is required.");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var transferNumber = await GenerateTransferNumberAsync();

        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            TransferNumber = transferNumber,
            FromWarehouseId = fromWarehouseId,
            ToWarehouseId = toWarehouseId,
            RequestedById = userId != Guid.Empty ? userId : null,
            RequestedByName = userName,
            RequestedDate = DateTime.UtcNow,
            PurchaseRequestId = request.PurchaseRequestId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        // Batch-load units + the balances for both warehouses up front
        // (avoids the previous per-line N+1 queries).
        var lineUnitIds = request.Lines.Select(l => l.UnitId).Distinct().ToList();
        var units = await _context.Units
            .Include(u => u.Product)
            .Where(u => lineUnitIds.Contains(u.Id) && u.IsActive)
            .ToListAsync();
        var unitsCache = units.ToDictionary(u => u.Id);

        var missingUnit = request.Lines.FirstOrDefault(l => !unitsCache.ContainsKey(l.UnitId));
        if (missingUnit != null)
            throw new InvalidOperationException($"Unit '{await GetUnitLabelAsync(missingUnit.UnitId)}' not found or inactive.");

        var balances = await _context.StockBalances
            .Where(sb => (sb.WarehouseId == fromWarehouseId || sb.WarehouseId == toWarehouseId)
                         && lineUnitIds.Contains(sb.UnitId))
            .ToListAsync();
        var sourceByUnit = balances.Where(b => b.WarehouseId == fromWarehouseId).ToDictionary(b => b.UnitId);
        var destByUnit = balances.Where(b => b.WarehouseId == toWarehouseId).ToDictionary(b => b.UnitId);

        foreach (var lineReq in request.Lines)
        {
            if (lineReq.Quantity <= 0)
                throw new InvalidOperationException("Transfer quantity must be greater than zero.");

            var unit = unitsCache[lineReq.UnitId];

            var baseUnitsTransferred = lineReq.Quantity * unit.Quantity;

            // Get source stock balance
            sourceByUnit.TryGetValue(lineReq.UnitId, out var sourceBalance);

            int sourceBeforeQty = sourceBalance?.AvailableQuantity ?? 0;

            if (sourceBeforeQty < baseUnitsTransferred)
                throw new InvalidOperationException(
                    $"Insufficient stock for unit {unit.Barcode} ({unit.Product?.NameEn}) in source warehouse. Available: {sourceBeforeQty}, Requested: {lineReq.Quantity} x {unit.Quantity} = {baseUnitsTransferred}.");

            // Get or create destination stock balance
            if (!destByUnit.TryGetValue(lineReq.UnitId, out var destBalance))
            {
                destBalance = new StockBalance
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = toWarehouseId,
                    UnitId = lineReq.UnitId,
                    AvailableQuantity = 0,
                    ReservedQuantity = 0,
                    InTransitQuantity = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StockBalances.Add(destBalance);
                destByUnit[lineReq.UnitId] = destBalance;
            }

            int destBeforeQty = destBalance.AvailableQuantity;

            // Update balances
            sourceBalance!.AvailableQuantity -= baseUnitsTransferred;
            sourceBalance.UpdatedAt = DateTime.UtcNow;

            destBalance.AvailableQuantity += baseUnitsTransferred;
            destBalance.UpdatedAt = DateTime.UtcNow;

            int sourceAfterQty = sourceBalance.AvailableQuantity;
            int destAfterQty = destBalance.AvailableQuantity;

            transfer.Lines.Add(new StockTransferLine
            {
                Id = Guid.NewGuid(),
                StockTransferId = transfer.Id,
                UnitId = lineReq.UnitId,
                Quantity = lineReq.Quantity,
                SourceQuantityBefore = sourceBeforeQty,
                SourceQuantityAfter = sourceAfterQty,
                DestinationQuantityBefore = destBeforeQty,
                DestinationQuantityAfter = destAfterQty,
                Notes = lineReq.Notes,
                CreatedAt = DateTime.UtcNow
            });

            // Inventory history: TransferOut from source
            _context.InventoryHistories.Add(new InventoryHistory
            {
                Id = Guid.NewGuid(),
                WarehouseId = fromWarehouseId,
                UnitId = lineReq.UnitId,
                ActionType = InventoryActionType.TransferOut,
                QuantityChange = -baseUnitsTransferred,
                AvailableQuantityBefore = sourceBeforeQty,
                AvailableQuantityAfter = sourceAfterQty,
                ReferenceType = "StockTransfer",
                ReferenceId = transfer.Id,
                PerformedBy = userName,
                PerformedAt = DateTime.UtcNow,
                Notes = $"Transfer {transfer.TransferNumber} - {unit.Product?.NameEn ?? unit.Barcode} ({lineReq.Quantity} x {unit.Quantity}) to {toWarehouse.NameEn}",
                CreatedAt = DateTime.UtcNow
            });

            // Inventory history: TransferIn to destination
            _context.InventoryHistories.Add(new InventoryHistory
            {
                Id = Guid.NewGuid(),
                WarehouseId = toWarehouseId,
                UnitId = lineReq.UnitId,
                ActionType = InventoryActionType.TransferIn,
                QuantityChange = baseUnitsTransferred,
                AvailableQuantityBefore = destBeforeQty,
                AvailableQuantityAfter = destAfterQty,
                ReferenceType = "StockTransfer",
                ReferenceId = transfer.Id,
                PerformedBy = userName,
                PerformedAt = DateTime.UtcNow,
                Notes = $"Transfer {transfer.TransferNumber} - {unit.Product?.NameEn ?? unit.Barcode} ({lineReq.Quantity} x {unit.Quantity}) from {fromWarehouse.NameEn}",
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.StockTransfers.Add(transfer);
        await _context.SaveChangesAsync();

        transferIdOut = transfer.Id;
        transferNumberOut = transfer.TransferNumber;
        userIdOut = userId;
        userNameOut = userName;
        fromWarehouseNameOut = fromWarehouse.NameEn;
        toWarehouseNameOut = toWarehouse.NameEn;
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
                EntityName = "StockTransfer",
                EntityId = transferIdOut.ToString(),
                Details = $"Created transfer {transferNumberOut} from {fromWarehouseNameOut} to {toWarehouseNameOut}"
            });
        }

        return (await GetByIdAsync(transferIdOut))!;
    }

    public async Task DeleteAsync(Guid id)
    {
        Guid transferIdOut = Guid.Empty;
        string transferNumber = string.Empty;
        Guid userIdOut = Guid.Empty;
        string? userNameOut = null;

        async Task ApplyAsync()
        {
        var transfer = await _context.StockTransfers
            .Include(t => t.Lines)
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException($"Stock transfer with ID {id} not found.");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        // Reverse stock balance changes
        foreach (var line in transfer.Lines)
        {
            var unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == line.UnitId);
            var baseUnitsTransferred = line.Quantity * (unit?.Quantity ?? 1);

            var sourceBalance = await _context.StockBalances
                .FirstOrDefaultAsync(sb => sb.WarehouseId == transfer.FromWarehouseId && sb.UnitId == line.UnitId);

            var destBalance = await _context.StockBalances
                .FirstOrDefaultAsync(sb => sb.WarehouseId == transfer.ToWarehouseId && sb.UnitId == line.UnitId);

            // Reverse source: add back
            if (sourceBalance != null)
            {
                var beforeQty = sourceBalance.AvailableQuantity;
                sourceBalance.AvailableQuantity += baseUnitsTransferred;
                sourceBalance.UpdatedAt = DateTime.UtcNow;

                _context.InventoryHistories.Add(new InventoryHistory
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = transfer.FromWarehouseId,
                    UnitId = line.UnitId,
                    ActionType = InventoryActionType.TransferIn,
                    QuantityChange = baseUnitsTransferred,
                    AvailableQuantityBefore = beforeQty,
                    AvailableQuantityAfter = sourceBalance.AvailableQuantity,
                    ReferenceType = "StockTransfer",
                    ReferenceId = transfer.Id,
                    PerformedBy = userName,
                    PerformedAt = DateTime.UtcNow,
                    Notes = $"Reversed transfer {transfer.TransferNumber} (deleted)",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Reverse destination: remove
            if (destBalance != null)
            {
                var beforeQty = destBalance.AvailableQuantity;
                destBalance.AvailableQuantity -= baseUnitsTransferred;
                if (destBalance.AvailableQuantity < 0)
                    throw new InvalidOperationException(
                        $"Cannot reverse transfer {transfer.TransferNumber}: reversal would make destination available quantity negative for unit {line.UnitId}. Current: {beforeQty}, Reversing: {baseUnitsTransferred}.");
                destBalance.UpdatedAt = DateTime.UtcNow;

                _context.InventoryHistories.Add(new InventoryHistory
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = transfer.ToWarehouseId,
                    UnitId = line.UnitId,
                    ActionType = InventoryActionType.TransferOut,
                    QuantityChange = -baseUnitsTransferred,
                    AvailableQuantityBefore = beforeQty,
                    AvailableQuantityAfter = destBalance.AvailableQuantity,
                    ReferenceType = "StockTransfer",
                    ReferenceId = transfer.Id,
                    PerformedBy = userName,
                    PerformedAt = DateTime.UtcNow,
                    Notes = $"Reversed transfer {transfer.TransferNumber} (deleted)",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _context.StockTransfers.Remove(transfer);
        await _context.SaveChangesAsync();

        transferIdOut = transfer.Id;
        transferNumber = transfer.TransferNumber;
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
                EntityName = "StockTransfer",
                EntityId = transferIdOut.ToString(),
                Details = $"Deleted transfer {transferNumber}"
            });
        }
    }

    private async Task<string> GenerateTransferNumberAsync()
    {
        // Atomic per-day counter — concurrency-safe.
        var prefix = $"TRF-{DateTime.UtcNow:yyyyMMdd}-";
        var nextSeq = await _numberSequence.NextAsync($"TRF-{DateTime.UtcNow:yyyyMMdd}");
        return $"{prefix}{nextSeq:D4}";
    }

    private static StockTransferDto MapToDto(StockTransfer t)
    {
        var isToCentral = t.ToWarehouse?.WarehouseType?.Code == WarehouseTypeCodes.CentralWarehouse;
        var transferType = isToCentral ? "ToCentral" : "FromCentral";
        var warehouse = isToCentral ? t.FromWarehouse : t.ToWarehouse;

        return new()
        {
            Id = t.Id,
            TransferNumber = t.TransferNumber,
            TransferType = transferType,
            WarehouseId = warehouse?.Id ?? Guid.Empty,
            WarehouseNameEn = warehouse?.NameEn ?? string.Empty,
            WarehouseNameAr = warehouse?.NameAr ?? string.Empty,
            FromWarehouseId = t.FromWarehouseId,
            FromWarehouseNameEn = t.FromWarehouse?.NameEn ?? string.Empty,
            FromWarehouseNameAr = t.FromWarehouse?.NameAr ?? string.Empty,
            ToWarehouseId = t.ToWarehouseId,
            ToWarehouseNameEn = t.ToWarehouse?.NameEn ?? string.Empty,
            ToWarehouseNameAr = t.ToWarehouse?.NameAr ?? string.Empty,
            RequestedById = t.RequestedById,
            RequestedByName = t.RequestedByName,
            RequestedDate = t.RequestedDate,
            Notes = t.Notes,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            Lines = t.Lines.Select(l => new StockTransferLineDto
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
                Quantity = l.Quantity,
                SourceQuantityBefore = l.SourceQuantityBefore,
                SourceQuantityAfter = l.SourceQuantityAfter,
                DestinationQuantityBefore = l.DestinationQuantityBefore,
                DestinationQuantityAfter = l.DestinationQuantityAfter,
                Notes = l.Notes
            }).ToList()
        };
    }
}
