using Application.Common.Models;
using Application.Features.Lookups;
using Application.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Application.Features.UserLogs;
using Domain.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.Services;

public class LookupService : ILookupService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;

    public LookupService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<LookupDto>> GetAllLookupsAsync(int pageNumber, int pageSize, string? parentCode = null, string? search = null, bool? isActive = null)
    {
        var query = _context.Lookups
            .Include(l => l.Parent)
            .AsQueryable();

        // Filter by parent code (e.g. "COUNTRY" to get all countries)
        if (!string.IsNullOrWhiteSpace(parentCode))
        {
            query = query.Where(l => l.Parent != null && l.Parent.Code == parentCode);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(l =>
                l.Code.ToLower().Contains(searchLower) ||
                l.NameEn.ToLower().Contains(searchLower) ||
                l.NameAr.ToLower().Contains(searchLower));
        }

        // Apply status filter
        if (isActive.HasValue)
        {
            query = query.Where(l => l.IsActive == isActive.Value);
        }

        // Order by latest updated/created first
        query = query.OrderByDescending(l => l.UpdatedAt ?? l.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LookupDto
            {
                Id = l.Id,
                Code = l.Code,
                NameEn = l.NameEn,
                NameAr = l.NameAr,
                ParentId = l.ParentId,
                ParentName = l.Parent != null ? l.Parent.NameEn : null,
                IsActive = l.IsActive
            })
            .ToListAsync();

        return new PaginatedList<LookupDto>(items, count, pageNumber, pageSize);
    }

    public async Task<List<LookupDto>> GetLookupsByParentAsync(string parentCode)
    {
        if (string.IsNullOrWhiteSpace(parentCode)) return new List<LookupDto>();

        return await _context.Lookups
            .Include(l => l.Parent)
            .Where(l => l.Parent != null && l.Parent.Code == parentCode)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LookupDto
            {
                Id = l.Id,
                Code = l.Code,
                NameEn = l.NameEn,
                NameAr = l.NameAr,
                ParentId = l.ParentId,
                ParentName = l.Parent != null ? l.Parent.NameEn : null,
                IsActive = l.IsActive
            })
            .ToListAsync();
    }

    public async Task<List<LookupDto>> GetRootLookupsAsync()
    {
        return await _context.Lookups
            .Where(l => l.ParentId == null)
            .OrderBy(l => l.NameEn)
            .Select(l => new LookupDto
            {
                Id = l.Id,
                Code = l.Code,
                NameEn = l.NameEn,
                NameAr = l.NameAr,
                ParentId = null,
                ParentName = null,
                IsActive = l.IsActive
            })
            .ToListAsync();
    }

    public async Task<LookupDto?> GetLookupByIdAsync(Guid id)
    {
        var lookup = await _context.Lookups
            .Include(l => l.Parent)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lookup == null) return null;

        return new LookupDto
        {
            Id = lookup.Id,
            Code = lookup.Code,
            NameEn = lookup.NameEn,
            NameAr = lookup.NameAr,
            ParentId = lookup.ParentId,
            ParentName = lookup.Parent?.NameEn,
            IsActive = lookup.IsActive
        };
    }

    public async Task<LookupDto> CreateLookupAsync(CreateLookupRequest request)
    {
        var lookup = new Lookup
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            ParentId = request.ParentId,
            IsActive = request.IsActive
        };

        _context.Lookups.Add(lookup);
        await _context.SaveChangesAsync();

        // Log action
        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Created,
                EntityName = "Lookup",
                EntityId = lookup.Id.ToString(),
                Details = null
            });
        }

        return await GetLookupByIdAsync(lookup.Id) ?? throw new InvalidOperationException("Failed to create lookup");
    }

    public async Task<LookupDto> UpdateLookupAsync(Guid id, UpdateLookupRequest request)
    {
        var lookup = await _context.Lookups.FindAsync(id);
        if (lookup == null) throw new KeyNotFoundException($"Lookup with ID {id} not found");

        lookup.Code = request.Code;
        lookup.NameEn = request.NameEn;
        lookup.NameAr = request.NameAr;
        lookup.ParentId = request.ParentId;
        lookup.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        // Log action
        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Updated,
                EntityName = "Lookup",
                EntityId = lookup.Id.ToString(),
                Details = null
            });
        }

        return await GetLookupByIdAsync(id) ?? throw new InvalidOperationException("Failed to update lookup");
    }

    public async Task DeleteLookupAsync(Guid id)
    {
        var lookup = await _context.Lookups.FindAsync(id);
        if (lookup != null)
        {
            if (await _context.Lookups.AnyAsync(l => l.ParentId == id))
                throw new InvalidOperationException("Cannot delete lookup: it has child lookups. Delete or move them first.");

            if (await _context.Warehouses.AnyAsync(w => w.WarehouseTypeId == id))
                throw new InvalidOperationException("Cannot delete lookup: it is used as a warehouse type.");

            _context.Lookups.Remove(lookup);
            await _context.SaveChangesAsync();

            // Log action
            var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
            if (currentUserId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = currentUserId,
                    UserName = currentUserName,
                    Action = AuditAction.Deleted,
                    EntityName = "Lookup",
                    EntityId = lookup.Id.ToString(),
                    Details = null
                });
            }
        }
    }

    public async Task<(bool CodeExists, bool NameEnExists, bool NameArExists)> CheckLookupExistsAsync(string code, string nameEn, string nameAr, Guid? excludeLookupId = null)
    {
        var query = _context.Lookups.AsQueryable();

        if (excludeLookupId.HasValue)
        {
            query = query.Where(l => l.Id != excludeLookupId.Value);
        }

        var codeExists = await query.AnyAsync(l => l.Code == code);
        var nameEnExists = await query.AnyAsync(l => l.NameEn == nameEn);
        var nameArExists = await query.AnyAsync(l => l.NameAr == nameAr);

        return (codeExists, nameEnExists, nameArExists);
    }
}
