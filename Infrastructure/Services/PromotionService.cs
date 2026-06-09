using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Promotions;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class PromotionService : IPromotionService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;

    public PromotionService(
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<PromotionDto>> GetAllPromotionsAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null)
    {
        var query = _context.Promotions
            .Include(p => p.PromotionUnits)
            .Include(p => p.PromotionCategories)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.NameEn.ToLower().Contains(searchLower) ||
                p.NameAr.Contains(search));
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        query = query.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);

        var count = await query.CountAsync();
        var promotions = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = promotions.Select(MapToDto).ToList();

        return new PaginatedList<PromotionDto>(items, count, pageNumber, pageSize);
    }

    public async Task<PromotionDto?> GetPromotionByIdAsync(Guid id)
    {
        var promotion = await _context.Promotions
            .Include(p => p.PromotionUnits)
            .Include(p => p.PromotionCategories)
            .FirstOrDefaultAsync(p => p.Id == id);

        return promotion == null ? null : MapToDto(promotion);
    }

    public async Task<PromotionDto> CreatePromotionAsync(CreatePromotionRequest request)
    {
        // Validate ApplyTo - AllSellingUnits and SpecificUnits are mutually exclusive
        ValidateApplyTo(request.ApplyTo);

        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            DescriptionEn = request.DescriptionEn,
            DescriptionAr = request.DescriptionAr,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            ApplyTo = request.ApplyTo,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        // Add unit associations
        foreach (var unitId in request.UnitIds)
        {
            promotion.PromotionUnits.Add(new PromotionUnit
            {
                Id = Guid.NewGuid(),
                PromotionId = promotion.Id,
                UnitId = unitId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Add category associations
        foreach (var categoryId in request.CategoryIds)
        {
            promotion.PromotionCategories.Add(new PromotionCategory
            {
                Id = Guid.NewGuid(),
                PromotionId = promotion.Id,
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.Promotions.Add(promotion);
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
                EntityName = "Promotion",
                EntityId = promotion.Id.ToString(),
                Details = null
            });
        }

        return MapToDto(promotion);
    }

    public async Task<PromotionDto> UpdatePromotionAsync(Guid id, UpdatePromotionRequest request)
    {
        // Validate ApplyTo - AllSellingUnits and SpecificUnits are mutually exclusive
        ValidateApplyTo(request.ApplyTo);

        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.Id == id);

        if (promotion == null)
        {
            throw new KeyNotFoundException($"Promotion with ID {id} not found.");
        }

        promotion.NameEn = request.NameEn;
        promotion.NameAr = request.NameAr;
        promotion.DescriptionEn = request.DescriptionEn;
        promotion.DescriptionAr = request.DescriptionAr;
        promotion.DiscountType = request.DiscountType;
        promotion.DiscountValue = request.DiscountValue;
        promotion.ApplyTo = request.ApplyTo;
        promotion.StartDateTime = request.StartDateTime;
        promotion.EndDateTime = request.EndDateTime;
        promotion.IsActive = request.IsActive;
        promotion.UpdatedAt = DateTime.UtcNow;

        // Hard-delete existing associations (bypasses the soft-delete interceptor in SaveChanges
        // to avoid unique-index violations when the same unit/category is re-added)
        await _context.PromotionUnits
            .IgnoreQueryFilters()
            .Where(pu => pu.PromotionId == id)
            .ExecuteDeleteAsync();

        await _context.PromotionCategories
            .IgnoreQueryFilters()
            .Where(pc => pc.PromotionId == id)
            .ExecuteDeleteAsync();

        // Add new unit associations
        foreach (var unitId in request.UnitIds)
        {
            _context.PromotionUnits.Add(new PromotionUnit
            {
                Id = Guid.NewGuid(),
                PromotionId = promotion.Id,
                UnitId = unitId,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Add new category associations
        foreach (var categoryId in request.CategoryIds)
        {
            _context.PromotionCategories.Add(new PromotionCategory
            {
                Id = Guid.NewGuid(),
                PromotionId = promotion.Id,
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow
            });
        }

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
                EntityName = "Promotion",
                EntityId = promotion.Id.ToString(),
                Details = null
            });
        }

        // Reload promotion with associations for return
        var updatedPromotion = await _context.Promotions
            .Include(p => p.PromotionUnits)
            .Include(p => p.PromotionCategories)
            .FirstAsync(p => p.Id == id);

        return MapToDto(updatedPromotion);
    }

    public async Task DeletePromotionAsync(Guid id)
    {
        var promotion = await _context.Promotions
            .FirstOrDefaultAsync(p => p.Id == id);

        if (promotion == null)
        {
            throw new KeyNotFoundException($"Promotion with ID {id} not found.");
        }

        // Remove associations directly
        var existingUnits = await _context.PromotionUnits
            .Where(pu => pu.PromotionId == id)
            .ToListAsync();
        _context.PromotionUnits.RemoveRange(existingUnits);

        var existingCategories = await _context.PromotionCategories
            .Where(pc => pc.PromotionId == id)
            .ToListAsync();
        _context.PromotionCategories.RemoveRange(existingCategories);

        _context.Promotions.Remove(promotion);
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
                EntityName = "Promotion",
                EntityId = id.ToString(),
                Details = null
            });
        }
    }

    public async Task<List<PromotionDto>> GetActivePromotionsAsync()
    {
        var now = DateTime.UtcNow;
        var promotions = await _context.Promotions
            .Include(p => p.PromotionUnits)
            .Include(p => p.PromotionCategories)
            .Where(p => p.IsActive &&
                        p.StartDateTime <= now &&
                        p.EndDateTime >= now)
            .ToListAsync();

        return promotions.Select(MapToDto).ToList();
    }

    public async Task<List<PromotionDto>> GetPromotionsForUnitAsync(Guid unitId)
    {
        var now = DateTime.UtcNow;
        
        // Get the unit to find its product and category
        var unit = await _context.Units
            .Include(u => u.Product)
            .FirstOrDefaultAsync(u => u.Id == unitId);
        if (unit == null)
        {
            return new List<PromotionDto>();
        }

        // Fetch active promotions first, then filter in memory for correct flag checking
        var allActivePromotions = await _context.Promotions
            .Include(p => p.PromotionUnits)
            .Include(p => p.PromotionCategories)
            .Where(p => p.IsActive &&
                        p.StartDateTime <= now &&
                        p.EndDateTime >= now)
            .ToListAsync();

        var promotions = allActivePromotions
            .Where(p => ((int)p.ApplyTo & (int)PromotionApplyTo.AllSellingUnits) != 0 ||
                        (((int)p.ApplyTo & (int)PromotionApplyTo.SpecificUnits) != 0 && p.PromotionUnits.Any(pu => pu.UnitId == unitId)) ||
                        (((int)p.ApplyTo & (int)PromotionApplyTo.Categories) != 0 && unit.Product?.CategoryId != null && p.PromotionCategories.Any(pc => pc.CategoryId == unit.Product.CategoryId.Value)))
            .ToList();

        return promotions.Select(MapToDto).ToList();
    }

    private static PromotionDto MapToDto(Promotion promotion)
    {
        return new PromotionDto
        {
            Id = promotion.Id,
            NameEn = promotion.NameEn,
            NameAr = promotion.NameAr,
            DescriptionEn = promotion.DescriptionEn,
            DescriptionAr = promotion.DescriptionAr,
            DiscountType = promotion.DiscountType,
            DiscountValue = promotion.DiscountValue,
            ApplyTo = promotion.ApplyTo,
            StartDateTime = promotion.StartDateTime,
            EndDateTime = promotion.EndDateTime,
            IsActive = promotion.IsActive,
            UnitIds = promotion.PromotionUnits.Select(pu => pu.UnitId).ToList(),
            CategoryIds = promotion.PromotionCategories.Select(pc => pc.CategoryId).ToList(),
            CreatedAt = promotion.CreatedAt,
            UpdatedAt = promotion.UpdatedAt
        };
    }

    private static void ValidateApplyTo(PromotionApplyTo applyTo)
    {
        // AllSellingUnits and SpecificUnits cannot be selected together
        if (applyTo.HasFlag(PromotionApplyTo.AllSellingUnits) && applyTo.HasFlag(PromotionApplyTo.SpecificUnits))
        {
            throw new ArgumentException("Cannot select both 'All Selling Units' and 'Specific Units' at the same time.");
        }
    }
}
