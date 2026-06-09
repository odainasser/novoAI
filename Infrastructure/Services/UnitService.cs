using System.Text.Json;
using Application.Common.Behaviors;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Units;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using FluentValidation.Results;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class UnitService : IUnitService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;

    public UnitService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<UnitDto>> GetAllAsync(int pageNumber, int pageSize, string? search = null, Guid? productId = null, Guid? unitOfMeasureId = null, bool? isActive = null, Guid? unitTypeId = null, Guid? categoryId = null, Guid? supplierId = null, Domain.Enums.ItemStatus? status = null)
    {
        var query = _context.Units
            .Include(s => s.Product)
            .Include(s => s.UnitOfMeasure)
            .Include(s => s.UnitUnitTypes).ThenInclude(ut => ut.UnitType)
            .Include(s => s.UnitSuppliers).ThenInclude(usb => usb.Supplier)
            .AsSplitQuery()
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s =>
                s.Barcode.Contains(search) ||
                (s.UnitOfMeasure != null && (
                    s.UnitOfMeasure.NameEn.Contains(search) ||
                    s.UnitOfMeasure.NameAr.Contains(search))) ||
                (s.Product != null && (
                    s.Product.NameEn.Contains(search) ||
                    s.Product.NameAr.Contains(search))));
        }

        if (productId.HasValue)
            query = query.Where(s => s.ProductId == productId.Value);

        if (unitOfMeasureId.HasValue)
            query = query.Where(s => s.UnitOfMeasureId == unitOfMeasureId.Value);

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);
        else if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);
        else
            query = query.Where(s => s.Status != Domain.Enums.ItemStatus.Rejected);

        if (unitTypeId.HasValue)
            query = query.Where(s => s.UnitUnitTypes.Any(ut => ut.UnitTypeId == unitTypeId.Value));

        if (categoryId.HasValue)
            query = query.Where(s => s.Product != null && s.Product.CategoryId == categoryId.Value);

        if (supplierId.HasValue)
            query = query.Where(s => s.UnitSuppliers.Any(us => us.SupplierId == supplierId.Value));

        query = query.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt);

        var count = await query.CountAsync();
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<UnitDto>(entities.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<UnitDto?> GetByIdAsync(Guid id)
    {
        var entity = await _context.Units
            .Include(s => s.Product)
            .Include(s => s.UnitOfMeasure)
            .Include(s => s.UnitUnitTypes).ThenInclude(ut => ut.UnitType)
            .Include(s => s.UnitSuppliers).ThenInclude(usb => usb.Supplier)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (entity == null) return null;
        return MapToDto(entity);
    }

    public async Task<UnitDto> CreateAsync(CreateUnitRequest request)
    {
        await EnsureUnitOfMeasureNotDuplicatedAsync(request.ProductId, request.UnitOfMeasureId);

        // Auto-generate a unique barcode
        var barcode = Guid.NewGuid().ToString("N")[..12].ToUpper();
        while (await CheckBarcodeExistsAsync(barcode))
            barcode = Guid.NewGuid().ToString("N")[..12].ToUpper();

        var entity = new Unit
        {
            Id = Guid.NewGuid(),
            UnitOfMeasureId = request.UnitOfMeasureId,
            ProductId = request.ProductId,
            Barcode = barcode,
            Quantity = request.Quantity,
            LowStockThreshold = request.LowStockThreshold,
            Status = Domain.Enums.ItemStatus.Draft,
            IsActive = request.Status.HasValue && request.Status.Value == Domain.Enums.ItemStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        _context.Units.Add(entity);

        // Add unit type associations
        foreach (var typeId in request.UnitTypeIds)
        {
            _context.Set<UnitUnitType>().Add(new UnitUnitType
            {
                UnitId = entity.Id,
                UnitTypeId = typeId
            });
        }

        // Resolve display names for the request
        var addProduct = await _context.Products
            .Where(p => p.Id == entity.ProductId)
            .Select(p => new { p.NameEn, p.NameAr })
            .FirstOrDefaultAsync();
        var addUom = await _context.Lookups
            .Where(l => l.Id == entity.UnitOfMeasureId)
            .Select(l => new { l.NameEn, l.NameAr })
            .FirstOrDefaultAsync();
        var displayName = string.IsNullOrEmpty(addUom?.NameEn)
            ? addProduct?.NameEn
            : $"{addProduct?.NameEn} ({addUom.NameEn})";
        var addUnitTypeNames = await _context.Lookups
            .Where(l => request.UnitTypeIds.Contains(l.Id))
            .Select(l => new { l.NameEn, l.NameAr })
            .ToListAsync();

        // Auto-create add request so it appears in the Requests module
        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        var newUnitData = new
        {
            entity.UnitOfMeasureId,
            UnitOfMeasureEn = addUom?.NameEn,
            UnitOfMeasureAr = addUom?.NameAr,
            entity.ProductId,
            ProductEn = addProduct?.NameEn,
            ProductAr = addProduct?.NameAr,
            entity.Barcode,
            entity.Quantity,
            entity.LowStockThreshold,
            entity.SellingPrice,
            entity.Cost,
            Status = (request.Status.HasValue ? request.Status.Value : entity.Status).ToString(),
            UnitTypeIds = request.UnitTypeIds,
            UnitTypesEn = string.Join(", ", addUnitTypeNames.Select(u => u.NameEn)),
            UnitTypesAr = string.Join(", ", addUnitTypeNames.Select(u => u.NameAr))
        };
        var addRequest = new Domain.Entities.Request
        {
            Id = Guid.NewGuid(),
            Type = Domain.Enums.RequestType.AddUnit,
            Status = Domain.Enums.RequestStatus.Pending,
            RequestedById = currentUserId,
            RequestedByName = currentUserName,
            UnitId = entity.Id,
            ProductName = displayName,
            NewDataJson = JsonSerializer.Serialize(newUnitData),
            CreatedAt = DateTime.UtcNow
        };
        _context.Requests.Add(addRequest);

        await _context.SaveChangesAsync();

        await LogAsync(AuditAction.Created, entity.Id);

        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Created,
                EntityName = "Request",
                EntityId = addRequest.Id.ToString(),
                Details = $"{addRequest.Type}: {addRequest.ProductName} ({addRequest.Status})"
            });
        }

        await _context.Entry(entity).Reference(s => s.Product).LoadAsync();
        await _context.Entry(entity).Reference(s => s.UnitOfMeasure).LoadAsync();
        await _context.Entry(entity).Collection(s => s.UnitUnitTypes).LoadAsync();
        foreach (var uut in entity.UnitUnitTypes)
            await _context.Entry(uut).Reference(ut => ut.UnitType).LoadAsync();
        await _context.Entry(entity).Collection(s => s.UnitSuppliers).LoadAsync();
        foreach (var usb in entity.UnitSuppliers)
            await _context.Entry(usb).Reference(x => x.Supplier).LoadAsync();
        return MapToDto(entity);
    }

    public async Task<UnitDto> UpdateAsync(Guid id, UpdateUnitRequest request)
    {
        await EnsureUnitOfMeasureNotDuplicatedAsync(request.ProductId, request.UnitOfMeasureId, id);

        var entity = await _context.Units
            .Include(s => s.Product)
            .Include(s => s.UnitUnitTypes)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Unit with ID {id} not found.");

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();

        // Draft units: update entity directly and refresh the pending AddUnit request
        if (entity.Status == Domain.Enums.ItemStatus.Draft)
        {
            // Update the entity fields directly
            entity.UnitOfMeasureId = request.UnitOfMeasureId;
            entity.ProductId = request.ProductId;
            entity.Quantity = request.Quantity;
            entity.LowStockThreshold = request.LowStockThreshold;
            if (request.Status.HasValue)
            {
                entity.IsActive = request.Status.Value == Domain.Enums.ItemStatus.Active;
            }
            entity.UpdatedAt = DateTime.UtcNow;

            // Replace unit type associations
            _context.Set<UnitUnitType>().RemoveRange(entity.UnitUnitTypes);
            foreach (var typeId in request.UnitTypeIds)
            {
                _context.Set<UnitUnitType>().Add(new UnitUnitType
                {
                    UnitId = entity.Id,
                    UnitTypeId = typeId
                });
            }

            // Update the pending AddUnit request's NewDataJson
            var pendingRequest = await _context.Requests
                .Where(r => r.UnitId == entity.Id
                         && r.Type == Domain.Enums.RequestType.AddUnit
                         && r.Status == Domain.Enums.RequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingRequest != null)
            {
                var addUom = await _context.Lookups
                    .Where(l => l.Id == request.UnitOfMeasureId)
                    .Select(l => new { l.NameEn, l.NameAr })
                    .FirstOrDefaultAsync();
                var addProduct = await _context.Products
                    .Where(p => p.Id == request.ProductId)
                    .Select(p => new { p.NameEn, p.NameAr })
                    .FirstOrDefaultAsync();
                var addUnitTypeNames = await _context.Lookups
                    .Where(l => request.UnitTypeIds.Contains(l.Id))
                    .Select(l => new { l.NameEn, l.NameAr })
                    .ToListAsync();

                var displayName = string.IsNullOrEmpty(addUom?.NameEn)
                    ? addProduct?.NameEn
                    : $"{addProduct?.NameEn} ({addUom.NameEn})";

                var intendedStatus = request.Status.HasValue ? request.Status.Value : entity.Status;
                var updatedData = new
                {
                    entity.UnitOfMeasureId,
                    UnitOfMeasureEn = addUom?.NameEn,
                    UnitOfMeasureAr = addUom?.NameAr,
                    entity.ProductId,
                    ProductEn = addProduct?.NameEn,
                    ProductAr = addProduct?.NameAr,
                    entity.Barcode,
                    entity.Quantity,
                    entity.LowStockThreshold,
                    entity.SellingPrice,
                    entity.Cost,
                    Status = intendedStatus.ToString(),
                    UnitTypeIds = request.UnitTypeIds,
                    UnitTypesEn = string.Join(", ", addUnitTypeNames.Select(u => u.NameEn)),
                    UnitTypesAr = string.Join(", ", addUnitTypeNames.Select(u => u.NameAr))
                };

                pendingRequest.NewDataJson = JsonSerializer.Serialize(updatedData);
                pendingRequest.ProductName = displayName;
            }

            await _context.SaveChangesAsync();
            await LogAsync(AuditAction.UpdatedDraft, entity.Id);

            if (pendingRequest != null && currentUserId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = currentUserId,
                    UserName = currentUserName,
                    Action = AuditAction.UpdatedRequest,
                    EntityName = "Request",
                    EntityId = pendingRequest.Id.ToString(),
                    Details = $"{pendingRequest.Type}: {pendingRequest.ProductName} ({pendingRequest.Status})"
                });
            }

            await _context.Entry(entity).Reference(s => s.Product).LoadAsync();
            await _context.Entry(entity).Reference(s => s.UnitOfMeasure).LoadAsync();
            await _context.Entry(entity).Collection(s => s.UnitUnitTypes).LoadAsync();
            foreach (var uut in entity.UnitUnitTypes)
                await _context.Entry(uut).Reference(ut => ut.UnitType).LoadAsync();
            await _context.Entry(entity).Collection(s => s.UnitSuppliers).LoadAsync();
            foreach (var usb in entity.UnitSuppliers)
                await _context.Entry(usb).Reference(x => x.Supplier).LoadAsync();
            return MapToDto(entity);
        }

        // Capture current state as old data — resolve relation names
        var oldUnitTypeIds = entity.UnitUnitTypes.Select(ut => ut.UnitTypeId).ToList();
        var oldUom = await _context.Lookups
            .Where(l => l.Id == entity.UnitOfMeasureId)
            .Select(l => new { l.NameEn, l.NameAr })
            .FirstOrDefaultAsync();
        var oldUnitTypeNames = await _context.Lookups
            .Where(l => oldUnitTypeIds.Contains(l.Id))
            .Select(l => new { l.NameEn, l.NameAr })
            .ToListAsync();
        var oldUnitData = new
        {
            entity.UnitOfMeasureId,
            UnitOfMeasureEn = oldUom?.NameEn,
            UnitOfMeasureAr = oldUom?.NameAr,
            entity.ProductId,
            ProductEn = entity.Product?.NameEn,
            ProductAr = entity.Product?.NameAr,
            entity.Barcode,
            entity.Quantity,
            entity.LowStockThreshold,
            entity.SellingPrice,
            entity.Cost,
            Status = entity.Status.ToString(),
            UnitTypeIds = oldUnitTypeIds,
            UnitTypesEn = string.Join(", ", oldUnitTypeNames.Select(u => u.NameEn)),
            UnitTypesAr = string.Join(", ", oldUnitTypeNames.Select(u => u.NameAr))
        };

        // Build new data — resolve relation names for incoming IDs
        var newUom = await _context.Lookups
            .Where(l => l.Id == request.UnitOfMeasureId)
            .Select(l => new { l.NameEn, l.NameAr })
            .FirstOrDefaultAsync();
        var newProductNames = await _context.Products
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.NameEn, p.NameAr })
            .FirstOrDefaultAsync();
        var newUnitTypeNames = await _context.Lookups
            .Where(l => request.UnitTypeIds.Contains(l.Id))
            .Select(l => new { l.NameEn, l.NameAr })
            .ToListAsync();
        var newStatus = request.Status.HasValue ? request.Status.Value : entity.Status;
        var newUnitData = new
        {
            UnitOfMeasureId = request.UnitOfMeasureId,
            UnitOfMeasureEn = newUom?.NameEn,
            UnitOfMeasureAr = newUom?.NameAr,
            ProductId = request.ProductId,
            ProductEn = newProductNames?.NameEn,
            ProductAr = newProductNames?.NameAr,
            Barcode = entity.Barcode,
            Quantity = request.Quantity,
            LowStockThreshold = request.LowStockThreshold,
            entity.SellingPrice,
            entity.Cost,
            Status = newStatus.ToString(),
            UnitTypeIds = request.UnitTypeIds,
            UnitTypesEn = string.Join(", ", newUnitTypeNames.Select(u => u.NameEn)),
            UnitTypesAr = string.Join(", ", newUnitTypeNames.Select(u => u.NameAr))
        };

        var resolvedProductName = newProductNames?.NameEn ?? entity.Product?.NameEn;
        var updateDisplayName = string.IsNullOrEmpty(newUom?.NameEn)
            ? resolvedProductName
            : $"{resolvedProductName} ({newUom.NameEn})";
        var updateRequest = new Domain.Entities.Request
        {
            Id = Guid.NewGuid(),
            Type = Domain.Enums.RequestType.UpdateUnit,
            Status = Domain.Enums.RequestStatus.Pending,
            RequestedById = currentUserId,
            RequestedByName = currentUserName,
            UnitId = entity.Id,
            ProductName = updateDisplayName,
            OldDataJson = JsonSerializer.Serialize(oldUnitData),
            NewDataJson = JsonSerializer.Serialize(newUnitData),
            CreatedAt = DateTime.UtcNow
        };
        _context.Requests.Add(updateRequest);

        await _context.SaveChangesAsync();

        await LogAsync(AuditAction.RequestedUpdate, entity.Id);

        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.RequestedUpdate,
                EntityName = "Request",
                EntityId = updateRequest.Id.ToString(),
                Details = $"{updateRequest.Type}: {updateRequest.ProductName} ({updateRequest.Status})"
            });
        }

        await _context.Entry(entity).Reference(s => s.Product).LoadAsync();
        await _context.Entry(entity).Reference(s => s.UnitOfMeasure).LoadAsync();
        await _context.Entry(entity).Collection(s => s.UnitUnitTypes).LoadAsync();
        foreach (var uut in entity.UnitUnitTypes)
            await _context.Entry(uut).Reference(ut => ut.UnitType).LoadAsync();
        await _context.Entry(entity).Collection(s => s.UnitSuppliers).LoadAsync();
        foreach (var usb in entity.UnitSuppliers)
            await _context.Entry(usb).Reference(x => x.Supplier).LoadAsync();
        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.Units.FindAsync(id)
            ?? throw new KeyNotFoundException($"Unit with ID {id} not found.");

        if (await _context.OrderItems.AnyAsync(oi => oi.UnitId == id))
            throw new InvalidOperationException("Cannot delete unit: it is linked to existing orders.");

        if (await _context.StockBalances.AnyAsync(sb => sb.UnitId == id &&
                (sb.AvailableQuantity != 0 || sb.ReservedQuantity != 0 || sb.InTransitQuantity != 0)))
            throw new InvalidOperationException("Cannot delete unit: it has stock balances in one or more warehouses.");

        if (await _context.Set<GoodsReceivingNoteLine>().AnyAsync(l => l.UnitId == id))
            throw new InvalidOperationException("Cannot delete unit: it is linked to goods receiving notes.");

        if (await _context.Set<StockAdjustmentLine>().AnyAsync(l => l.UnitId == id))
            throw new InvalidOperationException("Cannot delete unit: it is linked to stock adjustments.");

        if (await _context.Set<StockTransferLine>().AnyAsync(l => l.UnitId == id))
            throw new InvalidOperationException("Cannot delete unit: it is linked to stock transfers.");

        if (await _context.PromotionUnits.AnyAsync(pu => pu.UnitId == id))
            throw new InvalidOperationException("Cannot delete unit: it is linked to promotions.");

        // Remove any pending requests related to this unit
        var pendingRequests = await _context.Requests
            .Where(r => r.UnitId == id && r.Status == RequestStatus.Pending)
            .ToListAsync();
        if (pendingRequests.Any())
            _context.Requests.RemoveRange(pendingRequests);

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await LogAsync(AuditAction.Deleted, entity.Id);
    }

    public async Task<bool> CheckBarcodeExistsAsync(string barcode, Guid? excludeId = null)
    {
        var query = _context.Units.Where(s => s.Barcode == barcode && !s.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(s => s.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task<UnitDto> SetSellingDetailsAsync(Guid id, decimal sellingPrice, string sellingBarcode, int lowStockThreshold)
    {
        var entity = await _context.Units
            .Include(s => s.Product)
            .Include(s => s.UnitOfMeasure)
            .Include(s => s.UnitUnitTypes).ThenInclude(ut => ut.UnitType)
            .Include(s => s.UnitSuppliers).ThenInclude(usb => usb.Supplier)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Unit with ID {id} not found.");

        if (entity.Status != ItemStatus.Active)
            throw new InvalidOperationException("Cannot set selling details for a unit that is not approved (active).");

        entity.SellingPrice = sellingPrice;
        entity.SellingBarcode = sellingBarcode;
        entity.LowStockThreshold = lowStockThreshold;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await LogAsync(AuditAction.SetSellingDetails, entity.Id);

        return MapToDto(entity);
    }

    public async Task<UnitDto> SetLogisticsDetailsAsync(Guid id, decimal cost, List<UnitSupplierItem> suppliers, int lowStockThreshold)
    {
        var entity = await _context.Units
            .Include(s => s.Product)
            .Include(s => s.UnitOfMeasure)
            .Include(s => s.UnitUnitTypes).ThenInclude(ut => ut.UnitType)
            .Include(s => s.UnitSuppliers).ThenInclude(usb => usb.Supplier)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException($"Unit with ID {id} not found.");

        if (entity.Status != ItemStatus.Active)
            throw new InvalidOperationException("Cannot set logistics details for a unit that is not approved (active).");

        entity.Cost = cost;
        entity.LowStockThreshold = lowStockThreshold;
        entity.UpdatedAt = DateTime.UtcNow;

        // Replace unit suppliers
        _context.UnitSuppliers.RemoveRange(entity.UnitSuppliers);
        foreach (var item in suppliers)
        {
            _context.UnitSuppliers.Add(new UnitSupplier
            {
                UnitId = entity.Id,
                SupplierId = item.SupplierId,
                Barcode = item.Barcode
            });
        }

        await _context.SaveChangesAsync();

        await LogAsync(AuditAction.SetLogisticsDetails, entity.Id);

        // Reload for updated navigation properties
        await _context.Entry(entity).Collection(s => s.UnitSuppliers).LoadAsync();
        foreach (var usb in entity.UnitSuppliers)
            await _context.Entry(usb).Reference(x => x.Supplier).LoadAsync();

        return MapToDto(entity);
    }

    private static UnitDto MapToDto(Unit s) => new()
    {
        Id = s.Id,
        UnitOfMeasureId = s.UnitOfMeasureId,
        UnitOfMeasureNameEn = s.UnitOfMeasure?.NameEn,
        UnitOfMeasureNameAr = s.UnitOfMeasure?.NameAr,
        UnitTypes = s.UnitUnitTypes.Select(ut => new UnitTypeItemDto
        {
            Id = ut.UnitTypeId,
            Code = ut.UnitType?.Code,
            NameEn = ut.UnitType?.NameEn,
            NameAr = ut.UnitType?.NameAr
        }).ToList(),
        ProductId = s.ProductId,
        ProductNameEn = s.Product?.NameEn,
        ProductNameAr = s.Product?.NameAr,
        Barcode = s.Barcode,
        Quantity = s.Quantity,
        LowStockThreshold = s.LowStockThreshold,
        SellingPrice = s.SellingPrice,
        SellingBarcode = s.SellingBarcode,
        Cost = s.Cost,
        Suppliers = s.UnitSuppliers.Select(usb => new UnitSupplierDto
        {
            SupplierId = usb.SupplierId,
            SupplierNameEn = usb.Supplier?.NameEn,
            SupplierNameAr = usb.Supplier?.NameAr,
            Barcode = usb.Barcode
        }).ToList(),
        Status = s.Status,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    private async Task EnsureUnitOfMeasureNotDuplicatedAsync(Guid productId, Guid unitOfMeasureId, Guid? excludeUnitId = null)
    {
        var pendingDeleteUnitIds = await _context.Requests
            .Where(r => r.Type == RequestType.DeleteUnit && r.Status == RequestStatus.Pending && r.UnitId.HasValue)
            .Select(r => r.UnitId!.Value)
            .ToListAsync();

        var hasDuplicate = await _context.Units.AnyAsync(u =>
            u.ProductId == productId &&
            u.UnitOfMeasureId == unitOfMeasureId &&
            !u.IsDeleted &&
            u.Status != ItemStatus.Rejected &&
            !pendingDeleteUnitIds.Contains(u.Id) &&
            (!excludeUnitId.HasValue || u.Id != excludeUnitId.Value));

        if (!hasDuplicate)
            return;

        throw new ValidationException(new[]
        {
            new ValidationFailure(nameof(CreateUnitRequest.UnitOfMeasureId), "UnitOfMeasureAlreadyExistsForProduct")
        });
    }

    private async Task LogAsync(AuditAction action, Guid entityId)
    {
        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = action,
                EntityName = "Unit",
                EntityId = entityId.ToString(),
                Details = null
            });
        }
    }
}
