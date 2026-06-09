using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Suppliers;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class SupplierService : ISupplierService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;

    public SupplierService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<SupplierDto>> GetAllSuppliersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null)
    {
        var query = _context.Suppliers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(s => 
                s.NameEn.ToLower().Contains(searchLower) ||
                s.NameAr.ToLower().Contains(searchLower) ||
                (s.ContactPersonEn != null && s.ContactPersonEn.ToLower().Contains(searchLower)) ||
                (s.ContactPersonAr != null && s.ContactPersonAr.ToLower().Contains(searchLower)) ||
                (s.ContactEmail != null && s.ContactEmail.ToLower().Contains(searchLower)) ||
                (s.ContactPhone != null && s.ContactPhone.Contains(search)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        // Sort by latest updated/created first
        query = query.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt);

        var count = await query.CountAsync();
        var entities = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<SupplierDto>(entities.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<SupplierDto?> GetSupplierByIdAsync(Guid id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        return supplier == null ? null : MapToDto(supplier);
    }

    public async Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            ContactPersonEn = request.ContactPersonEn,
            ContactPersonAr = request.ContactPersonAr,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        // Log creation
        var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
        if (actorId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = actorId,
                UserName = actorName,
                Action = AuditAction.Created,
                EntityName = "Supplier",
                EntityId = supplier.Id.ToString(),
                Details = null
            });
        }

        return MapToDto(supplier);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request)
    {
        var supplier = await _context.Suppliers.FindAsync(id)
            ?? throw new KeyNotFoundException($"Supplier with ID '{id}' not found.");

        supplier.NameEn = request.NameEn;
        supplier.NameAr = request.NameAr;
        supplier.ContactPersonEn = request.ContactPersonEn;
        supplier.ContactPersonAr = request.ContactPersonAr;
        supplier.ContactEmail = request.ContactEmail;
        supplier.ContactPhone = request.ContactPhone;
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log update
        var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
        if (actorId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = actorId,
                UserName = actorName,
                Action = AuditAction.Updated,
                EntityName = "Supplier",
                EntityId = supplier.Id.ToString(),
                Details = null
            });
        }

        return MapToDto(supplier);
    }

    public async Task DeleteSupplierAsync(Guid id)
    {
        var supplier = await _context.Suppliers.FindAsync(id)
            ?? throw new KeyNotFoundException($"Supplier with ID '{id}' not found.");

        if (await _context.GoodsReceivingNotes.AnyAsync(g => g.SupplierId == id))
            throw new InvalidOperationException("Cannot delete supplier: it is linked to goods receiving notes.");

        if (await _context.Set<GoodsReceivingNoteLine>().AnyAsync(l => l.SupplierId == id))
            throw new InvalidOperationException("Cannot delete supplier: it is linked to goods receiving notes.");

        if (await _context.Set<UnitSupplier>().AnyAsync(us => us.SupplierId == id))
            throw new InvalidOperationException("Cannot delete supplier: it is linked to selling units.");

        var supplierNameEn = supplier.NameEn;
        var supplierNameAr = supplier.NameAr;

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync();

        // Log deletion
        var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
        if (actorId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = actorId,
                UserName = actorName,
                Action = AuditAction.Deleted,
                EntityName = "Supplier",
                EntityId = id.ToString(),
                Details = null
            });
        }
    }

    public async Task<bool> CheckSupplierExistsAsync(string nameEn, string nameAr, Guid? excludeSupplierId = null)
    {
        var query = _context.Suppliers.Where(s => 
            s.NameEn.ToLower() == nameEn.ToLower() || 
            s.NameAr.ToLower() == nameAr.ToLower());
        
        if (excludeSupplierId.HasValue)
        {
            query = query.Where(s => s.Id != excludeSupplierId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<bool> CheckSupplierEmailExistsAsync(string email, Guid? excludeSupplierId = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var query = _context.Suppliers.Where(s => 
            s.ContactEmail != null && s.ContactEmail.ToLower() == email.ToLower());
        
        if (excludeSupplierId.HasValue)
        {
            query = query.Where(s => s.Id != excludeSupplierId.Value);
        }

        return await query.AnyAsync();
    }

    private static SupplierDto MapToDto(Supplier supplier)
    {
        return new SupplierDto
        {
            Id = supplier.Id,
            NameEn = supplier.NameEn,
            NameAr = supplier.NameAr,
            ContactPersonEn = supplier.ContactPersonEn,
            ContactPersonAr = supplier.ContactPersonAr,
            ContactEmail = supplier.ContactEmail,
            ContactPhone = supplier.ContactPhone,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            UpdatedAt = supplier.UpdatedAt
        };
    }
}
