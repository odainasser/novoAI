using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.UserLogs;
using Application.Features.Warehouses;
using Application.Services;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class WarehouseService : IWarehouseService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;

    public WarehouseService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<WarehouseDto>> GetAllWarehousesAsync(
        int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseTypeId = null, Guid? branchId = null)
    {
        var query = _context.Warehouses
            .Include(w => w.WarehouseType)
            .Include(w => w.Branch)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(w =>
                w.NameEn.ToLower().Contains(s) ||
                w.NameAr.ToLower().Contains(s) ||
                (w.Address != null && w.Address.ToLower().Contains(s)) ||
                (w.ContactPerson != null && w.ContactPerson.ToLower().Contains(s)) ||
                (w.Email != null && w.Email.ToLower().Contains(s)));
        }

        if (isActive.HasValue)
            query = query.Where(w => w.IsActive == isActive.Value);

        if (warehouseTypeId.HasValue)
            query = query.Where(w => w.WarehouseTypeId == warehouseTypeId.Value);

        if (branchId.HasValue)
            query = query.Where(w => w.BranchId == branchId.Value);

        query = query.OrderByDescending(w => w.UpdatedAt ?? w.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<WarehouseDto>(items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<List<WarehouseDto>> GetActiveWarehousesAsync()
    {
        var warehouses = await _context.Warehouses
            .Include(w => w.WarehouseType)
            .Include(w => w.Branch)
            .Where(w => w.IsActive)
            .OrderBy(w => w.NameEn)
            .ToListAsync();

        return warehouses.Select(MapToDto).ToList();
    }

    public async Task<WarehouseDto?> GetWarehouseByIdAsync(Guid id)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.WarehouseType)
            .Include(w => w.Branch)
            .FirstOrDefaultAsync(w => w.Id == id);

        return warehouse == null ? null : MapToDto(warehouse);
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request)
    {
        // Enforce only one central warehouse
        var warehouseType = await _context.Set<Lookup>().FirstOrDefaultAsync(l => l.Id == request.WarehouseTypeId);
        if (warehouseType?.Code == WarehouseTypeCodes.CentralWarehouse)
        {
            var cwExists = await CheckCentralWarehouseExistsAsync();
            if (cwExists)
                throw new InvalidOperationException("Only one Central Warehouse is allowed in the system.");
        }

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            Address = request.Address,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            Email = request.Email,
            WarehouseTypeId = request.WarehouseTypeId,
            BranchId = request.BranchId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync();

        // reload nav props
        await _context.Entry(warehouse).Reference(w => w.WarehouseType).LoadAsync();
        if (warehouse.BranchId.HasValue)
            await _context.Entry(warehouse).Reference(w => w.Branch).LoadAsync();

        var (uid, uname) = await _currentUserService.GetCurrentUserAsync();
        if (uid != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = uid,
                UserName = uname,
                Action = AuditAction.Created,
                EntityName = "Warehouse",
                EntityId = warehouse.Id.ToString(),
                Details = null
            });
        }

        return MapToDto(warehouse);
    }

    public async Task<WarehouseDto> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.WarehouseType)
            .Include(w => w.Branch)
            .FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new KeyNotFoundException($"Warehouse with ID {id} not found.");

        // Enforce only one central warehouse
        var warehouseType = await _context.Set<Lookup>().FirstOrDefaultAsync(l => l.Id == request.WarehouseTypeId);
        if (warehouseType?.Code == WarehouseTypeCodes.CentralWarehouse)
        {
            var cwExists = await CheckCentralWarehouseExistsAsync(id);
            if (cwExists)
                throw new InvalidOperationException("Only one Central Warehouse is allowed in the system.");
        }

        warehouse.NameEn = request.NameEn;
        warehouse.NameAr = request.NameAr;
        warehouse.Address = request.Address;
        warehouse.ContactPerson = request.ContactPerson;
        warehouse.ContactPhone = request.ContactPhone;
        warehouse.Email = request.Email;
        warehouse.WarehouseTypeId = request.WarehouseTypeId;
        warehouse.BranchId = request.BranchId;
        warehouse.IsActive = request.IsActive;
        warehouse.UpdatedAt = DateTime.UtcNow;

        _context.Warehouses.Update(warehouse);
        await _context.SaveChangesAsync();

        // reload nav props
        await _context.Entry(warehouse).Reference(w => w.WarehouseType).LoadAsync();
        if (warehouse.BranchId.HasValue)
            await _context.Entry(warehouse).Reference(w => w.Branch).LoadAsync();

        var (uid, uname) = await _currentUserService.GetCurrentUserAsync();
        if (uid != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = uid,
                UserName = uname,
                Action = AuditAction.Updated,
                EntityName = "Warehouse",
                EntityId = warehouse.Id.ToString(),
                Details = null
            });
        }

        return MapToDto(warehouse);
    }

    public async Task DeleteWarehouseAsync(Guid id)
    {
        var warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id)
            ?? throw new KeyNotFoundException($"Warehouse with ID {id} not found.");

        _context.Warehouses.Remove(warehouse);
        await _context.SaveChangesAsync();

        var (uid, uname) = await _currentUserService.GetCurrentUserAsync();
        if (uid != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = uid,
                UserName = uname,
                Action = AuditAction.Deleted,
                EntityName = "Warehouse",
                EntityId = warehouse.Id.ToString(),
                Details = null
            });
        }
    }

    public async Task<bool> CheckWarehouseExistsAsync(string nameEn, string nameAr, Guid? excludeWarehouseId = null)
    {
        var query = _context.Warehouses.Where(w =>
            w.NameEn.ToLower() == nameEn.ToLower() ||
            w.NameAr.ToLower() == nameAr.ToLower());

        if (excludeWarehouseId.HasValue)
            query = query.Where(w => w.Id != excludeWarehouseId.Value);

        return await query.AnyAsync();
    }

    public async Task<bool> CheckCentralWarehouseExistsAsync(Guid? excludeWarehouseId = null)
    {
        var query = _context.Warehouses
            .Include(w => w.WarehouseType)
            .Where(w => w.WarehouseType != null && w.WarehouseType.Code == WarehouseTypeCodes.CentralWarehouse);

        if (excludeWarehouseId.HasValue)
            query = query.Where(w => w.Id != excludeWarehouseId.Value);

        return await query.AnyAsync();
    }

    private static WarehouseDto MapToDto(Warehouse w) => new()
    {
        Id = w.Id,
        NameEn = w.NameEn,
        NameAr = w.NameAr,
        Address = w.Address,
        ContactPerson = w.ContactPerson,
        ContactPhone = w.ContactPhone,
        Email = w.Email,
        WarehouseTypeId = w.WarehouseTypeId,
        WarehouseTypeNameEn = w.WarehouseType?.NameEn,
        WarehouseTypeNameAr = w.WarehouseType?.NameAr,
        WarehouseTypeCode = w.WarehouseType?.Code,
        BranchId = w.BranchId,
        BranchNameEn = w.Branch?.NameEn,
        BranchNameAr = w.Branch?.NameAr,
        IsActive = w.IsActive,
        CreatedAt = w.CreatedAt,
        UpdatedAt = w.UpdatedAt
    };
}
