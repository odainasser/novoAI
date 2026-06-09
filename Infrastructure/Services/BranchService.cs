using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Branches;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Constants;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaService _mediaService;

    public BranchService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService,
        IMediaService mediaService)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
        _mediaService = mediaService;
    }

    public async Task<PaginatedList<BranchDto>> GetAllBranchesAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null)
    {
        var query = _context.Branches.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(m =>
                m.NameEn.ToLower().Contains(searchLower) ||
                m.NameAr.ToLower().Contains(searchLower) ||
                (m.DescriptionEn != null && m.DescriptionEn.ToLower().Contains(searchLower)) ||
                (m.DescriptionAr != null && m.DescriptionAr.ToLower().Contains(searchLower)));
        }

        // Apply status filter
        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        // Sort by latest updated/created first
        query = query.OrderByDescending(m => m.UpdatedAt ?? m.CreatedAt);

        var count = await query.CountAsync();
        var branches = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<BranchDto>();
        foreach (var branch in branches)
        {
            items.Add(await MapToDtoAsync(branch));
        }

        return new PaginatedList<BranchDto>(items, count, pageNumber, pageSize);
    }

    public async Task<List<BranchDto>> GetActiveBranchesAsync()
    {
        var branches = await _context.Branches
            .Where(m => m.IsActive)
            .OrderBy(m => m.NameEn)
            .ToListAsync();

        var items = new List<BranchDto>();
        foreach (var branch in branches)
        {
            items.Add(await MapToDtoAsync(branch));
        }

        return items;
    }

    public async Task<List<BranchDto>> GetBranchesAssignedToUserAsync(Guid userId)
    {
        var assignedBranchIds = await _context.UserBranches
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.BranchId)
            .ToListAsync();

        if (assignedBranchIds.Count == 0)
        {
            return new List<BranchDto>();
        }

        var branches = await _context.Branches
            .Where(b => assignedBranchIds.Contains(b.Id) && b.IsActive)
            .OrderBy(b => b.NameEn)
            .ToListAsync();

        var items = new List<BranchDto>();
        foreach (var branch in branches)
        {
            items.Add(await MapToDtoAsync(branch));
        }

        return items;
    }

    public async Task<BranchDto?> GetBranchByIdAsync(Guid id)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(m => m.Id == id);
        return branch == null ? null : await MapToDtoAsync(branch);
    }

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request)
    {
        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            DescriptionEn = request.DescriptionEn,
            DescriptionAr = request.DescriptionAr,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Branches.Add(branch);
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
                EntityName = "Branch",
                EntityId = branch.Id.ToString(),
                Details = null
            });
        }

        return await MapToDtoAsync(branch);
    }

    public async Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchRequest request)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(m => m.Id == id);

        if (branch == null)
        {
            throw new KeyNotFoundException($"Branch with ID {id} not found.");
        }

        branch.NameEn = request.NameEn;
        branch.NameAr = request.NameAr;
        branch.DescriptionEn = request.DescriptionEn;
        branch.DescriptionAr = request.DescriptionAr;
        branch.IsActive = request.IsActive;
        branch.UpdatedAt = DateTime.UtcNow;

        _context.Branches.Update(branch);
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
                EntityName = "Branch",
                EntityId = branch.Id.ToString(),
                Details = null
            });
        }

        return await MapToDtoAsync(branch);
    }

    public async Task DeleteBranchAsync(Guid id)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(m => m.Id == id);

        if (branch == null)
        {
            throw new KeyNotFoundException($"Branch with ID {id} not found.");
        }

        if (await _context.Warehouses.AnyAsync(w => w.BranchId == id))
        {
            throw new InvalidOperationException("Cannot delete branch: it has linked warehouses (stores).");
        }

        if (await _context.Terminals.AnyAsync(t => t.BranchId == id))
        {
            throw new InvalidOperationException("Cannot delete branch: it has linked terminals.");
        }

        // Delete associated media
        var mediaList = await _mediaService.GetMediaForEntityAsync(id, EntityType.Branch, "image");
        foreach (var media in mediaList)
        {
            await _mediaService.DeleteMediaAsync(media.Id);
        }

        _context.Branches.Remove(branch);
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
                EntityName = "Branch",
                EntityId = branch.Id.ToString(),
                Details = null
            });
        }
    }

    public async Task<bool> CheckBranchExistsAsync(string nameEn, string nameAr, Guid? excludeBranchId = null)
    {
        var query = _context.Branches.Where(m =>
            m.NameEn.ToLower() == nameEn.ToLower() ||
            m.NameAr.ToLower() == nameAr.ToLower());

        if (excludeBranchId.HasValue)
        {
            query = query.Where(m => m.Id != excludeBranchId.Value);
        }

        return await query.AnyAsync();
    }

    // ===== Branch-scoping helpers (used by Branch Panel endpoints) =====

    public async Task<bool> IsUserAssignedToBranchAsync(Guid userId, Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.UserBranches
            .AnyAsync(ub => ub.UserId == userId && ub.BranchId == branchId, cancellationToken);
    }

    public async Task<BranchWarehouseInfo?> GetBranchWarehouseAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.Warehouses
            .Where(w => w.BranchId == branchId
                && w.IsActive
                && w.WarehouseType != null
                && w.WarehouseType.Code == WarehouseTypeCodes.BranchWarehouse)
            .Select(w => new BranchWarehouseInfo
            {
                Id = w.Id,
                NameEn = w.NameEn,
                NameAr = w.NameAr
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetWarehouseIdsForBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.Warehouses
            .Where(w => w.BranchId == branchId)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Guid>> GetUserIdsForBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await _context.UserBranches
            .Where(ub => ub.BranchId == branchId)
            .Select(ub => ub.UserId)
            .ToListAsync(cancellationToken);
    }

    private async Task<BranchDto> MapToDtoAsync(Branch branch)
    {
        var imageUrl = await GetBranchImageUrlAsync(branch.Id);

        return new BranchDto
        {
            Id = branch.Id,
            NameEn = branch.NameEn,
            NameAr = branch.NameAr,
            DescriptionEn = branch.DescriptionEn,
            DescriptionAr = branch.DescriptionAr,
            ImageUrl = imageUrl,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt,
            UpdatedAt = branch.UpdatedAt
        };
    }

    private async Task<string?> GetBranchImageUrlAsync(Guid branchId)
    {
        var mediaList = await _mediaService.GetMediaForEntityAsync(branchId, EntityType.Branch, "image");
        var media = mediaList.FirstOrDefault();
        return media != null ? _mediaService.GetMediaUrl(media) : null;
    }
}
