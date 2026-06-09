using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Categories;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaService _mediaService;

    public CategoryService(
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

    public async Task<PaginatedList<CategoryDto>> GetAllCategoriesAsync(int pageNumber, int pageSize, Guid? parentId = null, string? search = null, bool? isActive = null)
    {
        var query = _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Children)
            .AsQueryable();

        if (parentId.HasValue)
        {
            query = query.Where(c => c.ParentId == parentId.Value);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c =>
                c.NameEn.ToLower().Contains(searchLower) ||
                c.NameAr.ToLower().Contains(searchLower) ||
                (c.DescriptionEn != null && c.DescriptionEn.ToLower().Contains(searchLower)) ||
                (c.DescriptionAr != null && c.DescriptionAr.ToLower().Contains(searchLower)));
        }

        // Apply status filter
        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        // Sort by latest updated/created first
        query = query.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt);

        var count = await query.CountAsync();
        var categories = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Batch-fetch all related data for this page in bulk (instead of N+1 per category)
        var categoryIds = categories.Select(c => c.Id).ToList();
        var now = DateTime.UtcNow;

        var imagesByCategory = await _context.Media
            .Where(m => categoryIds.Contains(m.EntityId) && m.EntityType == EntityType.Category && m.CollectionName == "image")
            .ToListAsync();
        var imagesLookup = imagesByCategory.GroupBy(m => m.EntityId).ToDictionary(g => g.Key, g => g.ToList());

        var activePromotions = await _context.Promotions
            .Include(p => p.PromotionCategories)
            .Where(p => p.IsActive && p.StartDateTime <= now && p.EndDateTime >= now)
            .ToListAsync();

        var categoryPromotions = activePromotions
            .Where(p => ((int)p.ApplyTo & (int)PromotionApplyTo.Categories) != 0)
            .ToList();

        var items = new List<CategoryDto>();
        foreach (var category in categories)
        {
            var images = imagesLookup.GetValueOrDefault(category.Id);
            var image = images?.FirstOrDefault();
            var imageUrl = image != null ? _mediaService.GetMediaUrl(image) : null;

            var promotion = categoryPromotions
                .Where(p => p.PromotionCategories.Any(pc => pc.CategoryId == category.Id))
                .OrderByDescending(p => p.DiscountValue)
                .FirstOrDefault();

            var dto = new CategoryDto
            {
                Id = category.Id,
                NameEn = category.NameEn,
                NameAr = category.NameAr,
                DescriptionEn = category.DescriptionEn,
                DescriptionAr = category.DescriptionAr,
                ImageUrl = imageUrl,
                ParentId = category.ParentId,
                ParentNameEn = category.Parent?.NameEn,
                ParentNameAr = category.Parent?.NameAr,
                SortOrder = category.SortOrder,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt,
                ChildrenCount = category.Children?.Count ?? 0
            };

            if (promotion != null)
            {
                dto.HasPromotion = true;
                dto.PromotionId = promotion.Id;
                dto.PromotionNameEn = promotion.NameEn;
                dto.PromotionNameAr = promotion.NameAr;
                if (promotion.DiscountType == DiscountType.Percentage)
                {
                    dto.DiscountPercentage = (int)promotion.DiscountValue;
                }
            }

            items.Add(dto);
        }

        return new PaginatedList<CategoryDto>(items, count, pageNumber, pageSize);
    }

    public async Task<List<CategoryDto>> GetRootCategoriesAsync()
    {
        var categories = await _context.Categories
            .Include(c => c.Children)
            .Where(c => c.ParentId == null && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.NameEn)
            .ToListAsync();

        var items = new List<CategoryDto>();
        foreach (var category in categories)
        {
            items.Add(await MapToDtoAsync(category));
        }

        return items;
    }

    public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
    {
        var allCategories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.NameEn)
            .ToListAsync();

        return await BuildTreeAsync(allCategories, null);
    }

    private async Task<List<CategoryTreeDto>> BuildTreeAsync(List<Category> allCategories, Guid? parentId)
    {
        var result = new List<CategoryTreeDto>();
        var children = allCategories.Where(c => c.ParentId == parentId).ToList();

        foreach (var category in children)
        {
            var imageUrl = await GetCategoryImageUrlAsync(category.Id);
            result.Add(new CategoryTreeDto
            {
                Id = category.Id,
                NameEn = category.NameEn,
                NameAr = category.NameAr,
                DescriptionEn = category.DescriptionEn,
                DescriptionAr = category.DescriptionAr,
                ImageUrl = imageUrl,
                SortOrder = category.SortOrder,
                IsActive = category.IsActive,
                Children = await BuildTreeAsync(allCategories, category.Id)
            });
        }

        return result;
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        var category = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        return category == null ? null : await MapToDtoAsync(category);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            DescriptionEn = request.DescriptionEn,
            DescriptionAr = request.DescriptionAr,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
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
                EntityName = "Category",
                EntityId = category.Id.ToString(),
                Details = null
            });
        }

        return await MapToDtoAsync(category);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var category = await _context.Categories
            .Include(c => c.Parent)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            throw new KeyNotFoundException($"Category with ID {id} not found.");
        }

        // Prevent setting parent to itself or to one of its children
        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
            {
                throw new InvalidOperationException("Category cannot be its own parent.");
            }

            var descendantIds = await GetAllDescendantIdsAsync(id);
            if (descendantIds.Contains(request.ParentId.Value))
            {
                throw new InvalidOperationException("Category cannot have one of its descendants as parent.");
            }
        }

        category.NameEn = request.NameEn;
        category.NameAr = request.NameAr;
        category.DescriptionEn = request.DescriptionEn;
        category.DescriptionAr = request.DescriptionAr;
        category.ParentId = request.ParentId;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

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
                EntityName = "Category",
                EntityId = category.Id.ToString(),
                Details = null
            });
        }

        return await MapToDtoAsync(category);
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        var category = await _context.Categories
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            throw new KeyNotFoundException($"Category with ID {id} not found.");
        }

        if (category.Children.Any(c => !c.IsDeleted))
        {
            throw new InvalidOperationException("Cannot delete category: it has child categories. Delete or move them first.");
        }

        if (await _context.Products.AnyAsync(p => p.CategoryId == id))
        {
            throw new InvalidOperationException("Cannot delete category: it is linked to products.");
        }

        if (await _context.PromotionCategories.AnyAsync(pc => pc.CategoryId == id))
        {
            throw new InvalidOperationException("Cannot delete category: it is linked to promotions.");
        }

        // Delete associated media
        var mediaList = await _mediaService.GetMediaForEntityAsync(id, EntityType.Category);
        foreach (var media in mediaList)
        {
            await _mediaService.DeleteMediaAsync(media.Id);
        }

        _context.Categories.Remove(category);
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
                EntityName = "Category",
                EntityId = category.Id.ToString(),
                Details = null
            });
        }
    }

    public async Task<bool> CheckCategoryExistsAsync(string nameEn, string nameAr, Guid? excludeCategoryId = null)
    {
        var query = _context.Categories.AsQueryable();

        if (excludeCategoryId.HasValue)
        {
            query = query.Where(c => c.Id != excludeCategoryId.Value);
        }

        return await query.AnyAsync(c => c.NameEn == nameEn || c.NameAr == nameAr);
    }

    private async Task<HashSet<Guid>> GetAllDescendantIdsAsync(Guid categoryId)
    {
        var descendants = new HashSet<Guid>();
        var toProcess = new Queue<Guid>();
        toProcess.Enqueue(categoryId);

        while (toProcess.Count > 0)
        {
            var currentId = toProcess.Dequeue();
            var childIds = await _context.Categories
                .Where(c => c.ParentId == currentId)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var childId in childIds)
            {
                if (descendants.Add(childId))
                {
                    toProcess.Enqueue(childId);
                }
            }
        }

        return descendants;
    }

    private async Task<string?> GetCategoryImageUrlAsync(Guid categoryId)
    {
        var mediaList = await _mediaService.GetMediaForEntityAsync(categoryId, EntityType.Category, "image");
        var image = mediaList.FirstOrDefault();
        return image != null ? _mediaService.GetMediaUrl(image) : null;
    }

    private async Task<CategoryDto> MapToDtoAsync(Category category)
    {
        var imageUrl = await GetCategoryImageUrlAsync(category.Id);
        var now = DateTime.UtcNow;

        // Get promotion for this category
        var promotion = await GetPromotionForCategoryAsync(category.Id, now);

        var dto = new CategoryDto
        {
            Id = category.Id,
            NameEn = category.NameEn,
            NameAr = category.NameAr,
            DescriptionEn = category.DescriptionEn,
            DescriptionAr = category.DescriptionAr,
            ImageUrl = imageUrl,
            ParentId = category.ParentId,
            ParentNameEn = category.Parent?.NameEn,
            ParentNameAr = category.Parent?.NameAr,
            SortOrder = category.SortOrder,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
            ChildrenCount = category.Children?.Count ?? 0
        };

        // Apply promotion if found
        if (promotion != null)
        {
            dto.HasPromotion = true;
            dto.PromotionId = promotion.Id;
            dto.PromotionNameEn = promotion.NameEn;
            dto.PromotionNameAr = promotion.NameAr;
            if (promotion.DiscountType == DiscountType.Percentage)
            {
                dto.DiscountPercentage = (int)promotion.DiscountValue;
            }
        }

        return dto;
    }

    private async Task<Promotion?> GetPromotionForCategoryAsync(Guid categoryId, DateTime now)
    {
        // Get all active promotions that have this category
        var promotions = await _context.Promotions
            .Include(p => p.PromotionCategories)
            .Where(p => p.IsActive &&
                        p.StartDateTime <= now &&
                        p.EndDateTime >= now)
            .ToListAsync();

        // Filter in memory for the correct ApplyTo flag and category
        var promotion = promotions
            .Where(p => ((int)p.ApplyTo & (int)PromotionApplyTo.Categories) != 0 &&
                        p.PromotionCategories.Any(pc => pc.CategoryId == categoryId))
            .OrderByDescending(p => p.DiscountValue)
            .FirstOrDefault();

        return promotion;
    }
}
