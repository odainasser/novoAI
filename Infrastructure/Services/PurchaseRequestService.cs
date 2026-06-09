using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Features.PurchaseRequests;
using Application.Features.Requests;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Services;

/// <summary>
/// Originates and tracks Purchase Requests — the link between low stock and replenishment.
/// A PR never moves stock itself: on approval it is marked ready to convert, and conversion
/// reuses the existing <see cref="IGoodsReceivingService"/> (FromSupplier) or
/// <see cref="IStockTransferService"/> (FromCentralWarehouse) to perform the actual movement.
/// Submission/approval are routed through the existing <see cref="IRequestService"/> approval inbox.
/// </summary>
public class PurchaseRequestService : IPurchaseRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INumberSequenceService _numberSequence;
    private readonly IGoodsReceivingService _goodsReceivingService;
    private readonly IStockTransferService _stockTransferService;
    private readonly IRequestService _requestService;
    private readonly INotificationService _notificationService;
    private readonly IUserLogService _userLogService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PurchaseRequestService> _logger;

    public PurchaseRequestService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        INumberSequenceService numberSequence,
        IGoodsReceivingService goodsReceivingService,
        IStockTransferService stockTransferService,
        IRequestService requestService,
        INotificationService notificationService,
        IUserLogService userLogService,
        UserManager<ApplicationUser> userManager,
        ILogger<PurchaseRequestService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _numberSequence = numberSequence;
        _goodsReceivingService = goodsReceivingService;
        _stockTransferService = stockTransferService;
        _requestService = requestService;
        _notificationService = notificationService;
        _userLogService = userLogService;
        _userManager = userManager;
        _logger = logger;
    }

    // ===================== Queries =====================

    private IQueryable<PurchaseRequest> BaseQuery() => _context.PurchaseRequests
        .Include(pr => pr.RequestingWarehouse)
        .Include(pr => pr.Supplier)
        .Include(pr => pr.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.Product)
        .Include(pr => pr.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitOfMeasure);

    public async Task<PaginatedList<PurchaseRequestDto>> GetAllAsync(
        int pageNumber, int pageSize, string? search = null,
        PurchaseRequestStatus? status = null, PurchaseRequestSupplySource? supplySource = null,
        Guid? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = ApplyFilters(BaseQuery(), search, status, supplySource, warehouseId, fromDate, toDate)
            .OrderByDescending(pr => pr.CreatedAt);

        var count = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PaginatedList<PurchaseRequestDto>(items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<PaginatedList<PurchaseRequestDto>> GetByWarehouseIdsAsync(
        IEnumerable<Guid> warehouseIds, int pageNumber, int pageSize,
        PurchaseRequestStatus? status = null, PurchaseRequestSupplySource? supplySource = null)
    {
        var ids = warehouseIds?.Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0)
            return new PaginatedList<PurchaseRequestDto>(new List<PurchaseRequestDto>(), 0, pageNumber, pageSize);

        var query = BaseQuery().Where(pr => ids.Contains(pr.RequestingWarehouseId));
        query = ApplyFilters(query, null, status, supplySource, null, null, null);
        var ordered = query.OrderByDescending(pr => pr.CreatedAt);

        var count = await ordered.CountAsync();
        var items = await ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PaginatedList<PurchaseRequestDto>(items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    private static IQueryable<PurchaseRequest> ApplyFilters(
        IQueryable<PurchaseRequest> query, string? search,
        PurchaseRequestStatus? status, PurchaseRequestSupplySource? supplySource,
        Guid? warehouseId, DateTime? fromDate, DateTime? toDate)
    {
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(pr => pr.RequestNumber.Contains(search) || pr.RequestedByName.Contains(search));
        if (status.HasValue)
            query = query.Where(pr => pr.Status == status.Value);
        if (supplySource.HasValue)
            query = query.Where(pr => pr.SupplySource == supplySource.Value);
        if (warehouseId.HasValue)
            query = query.Where(pr => pr.RequestingWarehouseId == warehouseId.Value);
        if (fromDate.HasValue)
            query = query.Where(pr => pr.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(pr => pr.CreatedAt <= toDate.Value);
        return query;
    }

    public async Task<PurchaseRequestDto?> GetByIdAsync(Guid id)
    {
        var pr = await BaseQuery().FirstOrDefaultAsync(pr => pr.Id == id);
        return pr == null ? null : MapToDto(pr);
    }

    // ===================== Manual create / update =====================

    public async Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request)
    {
        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == request.RequestingWarehouseId && w.IsActive)
            ?? throw new InvalidOperationException("Requesting warehouse not found or inactive.");

        await ValidateSupplierAsync(request.SupplySource, request.SupplierId);

        var unitsCache = await LoadUnitsAsync(request.Lines.Select(l => l.UnitId));
        var balances = await LoadBalancesAsync(warehouse.Id, unitsCache.Keys);

        var pr = new PurchaseRequest
        {
            Id = Guid.NewGuid(),
            RequestNumber = await GeneratePurchaseRequestNumberAsync(),
            SupplySource = request.SupplySource,
            RequestingWarehouseId = warehouse.Id,
            SupplierId = request.SupplySource == PurchaseRequestSupplySource.FromSupplier ? request.SupplierId : null,
            Status = PurchaseRequestStatus.Draft,
            CreationMethod = PurchaseRequestCreationMethod.Manual,
            RequestedById = userId,
            RequestedByName = userName,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var lineReq in request.Lines)
        {
            if (!unitsCache.TryGetValue(lineReq.UnitId, out var unit))
                throw new InvalidOperationException($"Unit {lineReq.UnitId} not found or inactive.");

            pr.Lines.Add(new PurchaseRequestLine
            {
                Id = Guid.NewGuid(),
                PurchaseRequestId = pr.Id,
                UnitId = lineReq.UnitId,
                RequestedQuantity = lineReq.RequestedQuantity,
                SuggestedQuantity = null,
                CurrentAvailableQuantity = balances.TryGetValue(lineReq.UnitId, out var b) ? b.AvailableQuantity : 0,
                Notes = lineReq.Notes,
                CreatedAt = DateTime.UtcNow
            });
        }

        pr.TotalItems = pr.Lines.Count;

        _context.PurchaseRequests.Add(pr);
        await _context.SaveChangesAsync();

        await LogAsync(userId, userName, AuditAction.Created, pr, $"Created purchase request {pr.RequestNumber}");

        return (await GetByIdAsync(pr.Id))!;
    }

    public async Task<PurchaseRequestDto?> UpdateAsync(Guid id, UpdatePurchaseRequestRequest request)
    {
        var pr = await _context.PurchaseRequests.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id);
        if (pr == null) return null;

        if (pr.Status != PurchaseRequestStatus.Draft)
            throw new InvalidOperationException("Only draft purchase requests can be edited.");

        await ValidateSupplierAsync(request.SupplySource, request.SupplierId);

        var unitsCache = await LoadUnitsAsync(request.Lines.Select(l => l.UnitId));
        var balances = await LoadBalancesAsync(pr.RequestingWarehouseId, unitsCache.Keys);

        pr.SupplySource = request.SupplySource;
        pr.SupplierId = request.SupplySource == PurchaseRequestSupplySource.FromSupplier ? request.SupplierId : null;
        pr.Notes = request.Notes;
        pr.UpdatedAt = DateTime.UtcNow;

        _context.PurchaseRequestLines.RemoveRange(pr.Lines);
        pr.Lines.Clear();

        foreach (var lineReq in request.Lines)
        {
            if (!unitsCache.TryGetValue(lineReq.UnitId, out var unit))
                throw new InvalidOperationException($"Unit {lineReq.UnitId} not found or inactive.");

            pr.Lines.Add(new PurchaseRequestLine
            {
                Id = Guid.NewGuid(),
                PurchaseRequestId = pr.Id,
                UnitId = lineReq.UnitId,
                RequestedQuantity = lineReq.RequestedQuantity,
                SuggestedQuantity = null,
                CurrentAvailableQuantity = balances.TryGetValue(lineReq.UnitId, out var b) ? b.AvailableQuantity : 0,
                Notes = lineReq.Notes,
                CreatedAt = DateTime.UtcNow
            });
        }

        pr.TotalItems = pr.Lines.Count;

        await _context.SaveChangesAsync();
        return await GetByIdAsync(pr.Id);
    }

    // ===================== Auto-reorder proposals =====================

    public async Task<int> GenerateAutoReorderProposalsAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var warehousesQuery = _context.Warehouses
            .Include(w => w.WarehouseType)
            .Where(w => w.IsActive);
        if (warehouseId.HasValue)
            warehousesQuery = warehousesQuery.Where(w => w.Id == warehouseId.Value);

        var warehouses = await warehousesQuery.ToListAsync(cancellationToken);
        var created = 0;

        foreach (var warehouse in warehouses)
        {
            // Skip warehouses that already have an open auto-reorder proposal awaiting human review.
            var alreadyOpen = await _context.PurchaseRequests.AnyAsync(pr =>
                pr.RequestingWarehouseId == warehouse.Id &&
                pr.CreationMethod == PurchaseRequestCreationMethod.AutoReorder &&
                (pr.Status == PurchaseRequestStatus.Draft || pr.Status == PurchaseRequestStatus.Submitted),
                cancellationToken);
            if (alreadyOpen) continue;

            // Units at or below their reorder point in this warehouse.
            var lowStock = await (from sb in _context.StockBalances
                                  join u in _context.Units on sb.UnitId equals u.Id
                                  where sb.WarehouseId == warehouse.Id
                                        && u.IsActive
                                        && sb.AvailableQuantity <= u.LowStockThreshold
                                  select new { u.Id, u.LowStockThreshold, sb.AvailableQuantity })
                                 .ToListAsync(cancellationToken);

            if (lowStock.Count == 0) continue;

            var isCentral = warehouse.WarehouseType != null && warehouse.WarehouseType.Code == WarehouseTypeCodes.CentralWarehouse;
            var supplySource = isCentral
                ? PurchaseRequestSupplySource.FromSupplier
                : PurchaseRequestSupplySource.FromCentralWarehouse;

            var pr = new PurchaseRequest
            {
                Id = Guid.NewGuid(),
                RequestNumber = await GeneratePurchaseRequestNumberAsync(),
                SupplySource = supplySource,
                RequestingWarehouseId = warehouse.Id,
                SupplierId = null, // for FromSupplier, supplier is resolved per-line from the unit's UnitSupplier at conversion
                Status = PurchaseRequestStatus.Draft,
                CreationMethod = PurchaseRequestCreationMethod.AutoReorder,
                RequestedById = userId,
                RequestedByName = string.IsNullOrWhiteSpace(userName) ? "System" : userName,
                Notes = "Auto-generated from reorder point.",
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in lowStock)
            {
                // Order up to 2× the threshold (simple, transparent reorder rule).
                var suggested = CalculateReorderQuantity(item.LowStockThreshold, item.AvailableQuantity);
                if (suggested <= 0) continue;

                pr.Lines.Add(new PurchaseRequestLine
                {
                    Id = Guid.NewGuid(),
                    PurchaseRequestId = pr.Id,
                    UnitId = item.Id,
                    RequestedQuantity = suggested,
                    SuggestedQuantity = suggested,
                    CurrentAvailableQuantity = item.AvailableQuantity,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (pr.Lines.Count == 0) continue;

            pr.TotalItems = pr.Lines.Count;
            _context.PurchaseRequests.Add(pr);
            await _context.SaveChangesAsync(cancellationToken);
            created++;

            await NotifyProposalAsync(pr, warehouse, cancellationToken);
        }

        return created;
    }

    // ===================== Workflow: submit / approve / reject =====================

    public async Task<PurchaseRequestDto?> SubmitAsync(Guid id)
    {
        var pr = await _context.PurchaseRequests.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id);
        if (pr == null) return null;

        if (pr.Status != PurchaseRequestStatus.Draft)
            throw new InvalidOperationException("Only draft purchase requests can be submitted.");
        if (pr.Lines.Count == 0)
            throw new InvalidOperationException("Cannot submit a purchase request with no line items.");

        // Mirror into the unified approvals inbox so PRs are reviewed alongside other sensitive operations.
        var mirror = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.AddPurchaseRequest,
            Status = RequestStatus.Pending,
            RequestedById = pr.RequestedById,
            RequestedByName = pr.RequestedByName,
            ProductName = pr.RequestNumber,
            Note = pr.Notes,
            NewDataJson = JsonSerializer.Serialize(new { PurchaseRequestId = pr.Id, pr.RequestNumber }),
            CreatedAt = DateTime.UtcNow
        };
        _context.Requests.Add(mirror);

        pr.Status = PurchaseRequestStatus.Submitted;
        pr.SubmittedAt = DateTime.UtcNow;
        pr.LinkedRequestId = mirror.Id;
        pr.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await NotifyApproversOnSubmitAsync(pr);

        return await GetByIdAsync(pr.Id);
    }

    public async Task<PurchaseRequestDto?> ApproveAsync(Guid id, string? note = null)
        => await ReviewAsync(id, true, note);

    public async Task<PurchaseRequestDto?> RejectAsync(Guid id, string? reason = null)
        => await ReviewAsync(id, false, reason);

    private async Task<PurchaseRequestDto?> ReviewAsync(Guid id, bool approve, string? note)
    {
        var pr = await _context.PurchaseRequests.FirstOrDefaultAsync(p => p.Id == id);
        if (pr == null) return null;

        if (pr.Status != PurchaseRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted purchase requests can be reviewed.");

        if (pr.LinkedRequestId.HasValue)
        {
            // Route through the existing approval workflow; its AddPurchaseRequest dispatch arm
            // flips this PR's status and notifies the requester.
            await _requestService.ReviewRequestAsync(pr.LinkedRequestId.Value,
                new ReviewRequestDto { Approve = approve, ReviewNote = note });
        }
        else
        {
            // Defensive fallback for PRs without a mirror row.
            var (userId, userName) = await _currentUserService.GetCurrentUserAsync();
            if (approve)
            {
                pr.Status = PurchaseRequestStatus.Approved;
                pr.ApprovedById = userId;
                pr.ApprovedByName = userName;
                pr.ApprovedAt = DateTime.UtcNow;
            }
            else
            {
                pr.Status = PurchaseRequestStatus.Rejected;
                pr.RejectedById = userId;
                pr.RejectedByName = userName;
                pr.RejectedAt = DateTime.UtcNow;
                pr.RejectReason = note;
            }
            pr.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return await GetByIdAsync(pr.Id);
    }

    // ===================== Conversion =====================

    public async Task<PurchaseRequestDto?> ConvertAsync(Guid id)
    {
        var pr = await _context.PurchaseRequests
            .Include(p => p.Lines).ThenInclude(l => l.Unit).ThenInclude(u => u.UnitSuppliers)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pr == null) return null;

        if (pr.Status != PurchaseRequestStatus.Approved)
            throw new InvalidOperationException("Only approved purchase requests can be converted.");
        if (pr.Lines.Count == 0)
            throw new InvalidOperationException("Cannot convert a purchase request with no line items.");

        if (pr.SupplySource == PurchaseRequestSupplySource.FromSupplier)
        {
            var grnRequest = new CreateGoodsReceivingNoteRequest
            {
                PurchaseOrderReference = pr.RequestNumber,
                ReceivedDate = DateTime.UtcNow,
                Notes = pr.Notes,
                PurchaseRequestId = pr.Id,
                Lines = pr.Lines.Select(l => new CreateGoodsReceivingNoteLineRequest
                {
                    UnitId = l.UnitId,
                    SupplierId = ResolveSupplierForLine(pr, l),
                    Cost = l.Unit?.Cost ?? 0,
                    ReceivedQuantity = l.RequestedQuantity,
                    Notes = l.Notes
                }).ToList()
            };

            var grn = await _goodsReceivingService.CreateAsync(grnRequest);

            pr.ConvertedDocumentType = Domain.Enums.ConvertedDocumentType.GoodsReceivingNote;
            pr.ConvertedDocumentId = grn.Id;
            pr.ConvertedDocumentReference = grn.GRNNumber;
        }
        else // FromCentralWarehouse
        {
            var transferRequest = new CreateStockTransferRequest
            {
                WarehouseId = pr.RequestingWarehouseId,
                TransferType = "FromCentral",
                Notes = pr.Notes,
                PurchaseRequestId = pr.Id,
                Lines = pr.Lines.Select(l => new CreateStockTransferLineRequest
                {
                    UnitId = l.UnitId,
                    Quantity = l.RequestedQuantity,
                    Notes = l.Notes
                }).ToList()
            };

            var transfer = await _stockTransferService.CreateAsync(transferRequest);

            pr.ConvertedDocumentType = Domain.Enums.ConvertedDocumentType.StockTransfer;
            pr.ConvertedDocumentId = transfer.Id;
            pr.ConvertedDocumentReference = transfer.TransferNumber;
        }

        pr.Status = PurchaseRequestStatus.Converted;
        pr.ConvertedAt = DateTime.UtcNow;
        pr.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetByIdAsync(pr.Id);
    }

    private static Guid ResolveSupplierForLine(PurchaseRequest pr, PurchaseRequestLine line)
    {
        if (pr.SupplierId.HasValue && pr.SupplierId.Value != Guid.Empty)
            return pr.SupplierId.Value;

        var unitSupplier = line.Unit?.UnitSuppliers?.FirstOrDefault();
        if (unitSupplier != null)
            return unitSupplier.SupplierId;

        throw new InvalidOperationException(
            $"No supplier could be resolved for unit '{line.Unit?.Barcode ?? line.UnitId.ToString()}'. Set a supplier on the purchase request or link the unit to a supplier.");
    }

    // ===================== Cancel =====================

    public async Task<PurchaseRequestDto?> CancelAsync(Guid id)
    {
        var pr = await _context.PurchaseRequests.FirstOrDefaultAsync(p => p.Id == id);
        if (pr == null) return null;

        if (pr.Status != PurchaseRequestStatus.Draft && pr.Status != PurchaseRequestStatus.Submitted)
            throw new InvalidOperationException("Only draft or submitted purchase requests can be cancelled.");

        // Withdraw the mirror approval row from the inbox if it is still pending.
        if (pr.LinkedRequestId.HasValue)
        {
            var mirror = await _context.Requests.FirstOrDefaultAsync(r => r.Id == pr.LinkedRequestId.Value);
            if (mirror != null && mirror.Status == RequestStatus.Pending)
                _context.Requests.Remove(mirror);
        }

        pr.Status = PurchaseRequestStatus.Cancelled;
        pr.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await GetByIdAsync(pr.Id);
    }

    /// <summary>
    /// Reorder rule: restock up to twice the reorder point. Simple and explainable —
    /// suggested = max(0, 2 × threshold − available). No forecasting.
    /// </summary>
    public static int CalculateReorderQuantity(int threshold, int available)
        => Math.Max(0, (2 * threshold) - available);

    // ===================== Helpers =====================

    private async Task ValidateSupplierAsync(PurchaseRequestSupplySource source, Guid? supplierId)
    {
        if (source != PurchaseRequestSupplySource.FromSupplier || supplierId is null || supplierId == Guid.Empty)
            return;

        var exists = await _context.Suppliers.AnyAsync(s => s.Id == supplierId.Value && s.IsActive);
        if (!exists)
            throw new InvalidOperationException("Supplier not found or inactive.");
    }

    private async Task<Dictionary<Guid, Unit>> LoadUnitsAsync(IEnumerable<Guid> unitIds)
    {
        var ids = unitIds.Distinct().ToList();
        var units = await _context.Units
            .Include(u => u.Product)
            .Include(u => u.UnitOfMeasure)
            .Where(u => ids.Contains(u.Id) && u.IsActive)
            .ToListAsync();
        return units.ToDictionary(u => u.Id);
    }

    private async Task<Dictionary<Guid, StockBalance>> LoadBalancesAsync(Guid warehouseId, IEnumerable<Guid> unitIds)
    {
        var ids = unitIds.Distinct().ToList();
        var balances = await _context.StockBalances
            .Where(sb => sb.WarehouseId == warehouseId && ids.Contains(sb.UnitId))
            .ToListAsync();
        return balances.ToDictionary(sb => sb.UnitId);
    }

    private async Task<string> GeneratePurchaseRequestNumberAsync()
    {
        var prefix = $"PR-{DateTime.UtcNow:yyyyMMdd}-";
        var nextSeq = await _numberSequence.NextAsync($"PR-{DateTime.UtcNow:yyyyMMdd}");
        return $"{prefix}{nextSeq:D4}";
    }

    private async Task NotifyApproversOnSubmitAsync(PurchaseRequest pr)
    {
        try
        {
            var recipients = await ResolveApproverIdsAsync();
            if (recipients.Count == 0) return;

            await _notificationService.SendBulkAsync(
                recipients,
                NotificationType.PurchaseRequestSubmitted,
                titleEn: $"Purchase request {pr.RequestNumber} awaiting approval",
                titleAr: $"طلب شراء {pr.RequestNumber} بانتظار الموافقة",
                bodyEn: $"Submitted by {pr.RequestedByName}. Review and approve to convert it.",
                bodyAr: $"تم الإرسال بواسطة {pr.RequestedByName}. راجع ووافق لتحويله.",
                link: "/admin/inventory/purchase-requests");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify approvers of submitted purchase request {Id}", pr.Id);
        }
    }

    private async Task NotifyProposalAsync(PurchaseRequest pr, Warehouse warehouse, CancellationToken cancellationToken)
    {
        try
        {
            var recipients = await ResolveApproverIdsAsync();

            if (warehouse.BranchId.HasValue)
            {
                var branchUserIds = await (from ub in _context.UserBranches
                                           join u in _context.Set<ApplicationUser>() on ub.UserId equals u.Id
                                           where ub.BranchId == warehouse.BranchId.Value && u.IsActive && !u.IsDeleted
                                           select u.Id).ToListAsync(cancellationToken);
                foreach (var uid in branchUserIds) recipients.Add(uid);
            }

            if (recipients.Count == 0) return;

            await _notificationService.SendBulkAsync(
                recipients,
                NotificationType.PurchaseRequestProposed,
                titleEn: $"Reorder proposal {pr.RequestNumber} ready for review",
                titleAr: $"اقتراح إعادة طلب {pr.RequestNumber} جاهز للمراجعة",
                bodyEn: $"{pr.Lines.Count} item(s) at {warehouse.NameEn} are at or below their reorder point. Review and submit.",
                bodyAr: $"{pr.Lines.Count} صنف في {warehouse.NameAr} عند نقطة إعادة الطلب أو أقل. راجع وأرسل.",
                link: "/admin/inventory/purchase-requests",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify managers of auto-reorder proposal {Id}", pr.Id);
        }
    }

    private async Task<HashSet<Guid>> ResolveApproverIdsAsync()
    {
        var recipients = new HashSet<Guid>();
        var admins = await _userManager.GetUsersInRoleAsync(Roles.Administrator);
        foreach (var a in admins)
            if (a.IsActive && !a.IsDeleted) recipients.Add(a.Id);
        return recipients;
    }

    private async Task LogAsync(Guid userId, string? userName, AuditAction action, PurchaseRequest pr, string details)
    {
        if (userId == Guid.Empty) return;
        try
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = "PurchaseRequest",
                EntityId = pr.Id.ToString(),
                Details = details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write user log for purchase request {Id}", pr.Id);
        }
    }

    private static PurchaseRequestDto MapToDto(PurchaseRequest pr) => new()
    {
        Id = pr.Id,
        RequestNumber = pr.RequestNumber,
        SupplySource = pr.SupplySource,
        Status = pr.Status,
        CreationMethod = pr.CreationMethod,
        RequestingWarehouseId = pr.RequestingWarehouseId,
        RequestingWarehouseNameEn = pr.RequestingWarehouse?.NameEn ?? string.Empty,
        RequestingWarehouseNameAr = pr.RequestingWarehouse?.NameAr ?? string.Empty,
        RequestingBranchId = pr.RequestingWarehouse?.BranchId,
        SupplierId = pr.SupplierId,
        SupplierNameEn = pr.Supplier?.NameEn,
        SupplierNameAr = pr.Supplier?.NameAr,
        RequestedById = pr.RequestedById,
        RequestedByName = pr.RequestedByName,
        SubmittedAt = pr.SubmittedAt,
        ApprovedById = pr.ApprovedById,
        ApprovedByName = pr.ApprovedByName,
        ApprovedAt = pr.ApprovedAt,
        RejectedById = pr.RejectedById,
        RejectedByName = pr.RejectedByName,
        RejectedAt = pr.RejectedAt,
        RejectReason = pr.RejectReason,
        ConvertedDocumentType = pr.ConvertedDocumentType,
        ConvertedDocumentId = pr.ConvertedDocumentId,
        ConvertedDocumentReference = pr.ConvertedDocumentReference,
        ConvertedAt = pr.ConvertedAt,
        LinkedRequestId = pr.LinkedRequestId,
        TotalItems = pr.TotalItems,
        Notes = pr.Notes,
        CreatedAt = pr.CreatedAt,
        UpdatedAt = pr.UpdatedAt,
        Lines = pr.Lines.Select(l => new PurchaseRequestLineDto
        {
            Id = l.Id,
            UnitId = l.UnitId,
            UnitBarcode = l.Unit?.Barcode ?? string.Empty,
            ProductId = l.Unit?.ProductId ?? Guid.Empty,
            ProductNameEn = l.Unit?.Product?.NameEn ?? string.Empty,
            ProductNameAr = l.Unit?.Product?.NameAr ?? string.Empty,
            ProductCode = l.Unit?.Product?.Code ?? string.Empty,
            UnitOfMeasureNameEn = l.Unit?.UnitOfMeasure?.NameEn ?? string.Empty,
            UnitOfMeasureNameAr = l.Unit?.UnitOfMeasure?.NameAr ?? string.Empty,
            UnitBaseQuantity = l.Unit?.Quantity ?? 1,
            RequestedQuantity = l.RequestedQuantity,
            SuggestedQuantity = l.SuggestedQuantity,
            CurrentAvailableQuantity = l.CurrentAvailableQuantity,
            Notes = l.Notes
        }).ToList()
    };
}
