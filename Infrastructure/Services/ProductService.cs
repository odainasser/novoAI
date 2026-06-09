using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Products;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaService _mediaService;

    public ProductService(
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

    public async Task<PaginatedList<ProductDto>> GetAllProductsAsync(int pageNumber, int pageSize, string? search = null, Guid? categoryId = null, bool? isActive = null, Guid? warehouseId = null, bool? onlyWithStock = null, Domain.Enums.ItemStatus? status = null)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.NameEn.Contains(search) ||
                p.NameAr.Contains(search) ||
                p.Code.Contains(search) ||
                _context.Units.Any(u =>
                    u.ProductId == p.Id &&
                    (u.Status == ItemStatus.Active || u.Status == ItemStatus.Inactive || u.Status == ItemStatus.Draft) &&
                    (u.Barcode.Contains(search) ||
                     u.SellingBarcode.Contains(search) ||
                     u.UnitSuppliers.Any(us => us.Barcode.Contains(search)))));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value && p.IsActive == (status.Value == ItemStatus.Active));
        }
        else if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }
        else
        {
            // Exclude rejected items from unfiltered lists (same behaviour as inactive)
            query = query.Where(p => p.Status != ItemStatus.Rejected);
        }

        // Filter products that have available stock in the specified warehouse
        if (onlyWithStock == true)
        {
            if (warehouseId.HasValue)
            {
                query = query.Where(p => _context.StockBalances
                    .Any(sb => sb.Unit.ProductId == p.Id && sb.Unit.IsActive && sb.Unit.Status == ItemStatus.Active && sb.WarehouseId == warehouseId.Value && sb.AvailableQuantity > 0));
            }
            else
            {
                // No warehouse specified with onlyWithStock — return empty result
                return new PaginatedList<ProductDto>(new List<ProductDto>(), 0, pageNumber, pageSize);
            }
        }

        // Sort by latest updated/created first
        query = query.OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);

        var count = await query.CountAsync();
        var products = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Resolve warehouse ID for stock scoping
        Guid? branchWarehouseId = warehouseId;

        // Batch-fetch all related data for this page in bulk (instead of N+1 per product)
        var productIds = products.Select(p => p.Id).ToList();

        var imagesByProduct = await _context.Media
            .Where(m => productIds.Contains(m.EntityId) && m.EntityType == EntityType.Product && m.CollectionName == "image")
            .AsNoTracking()
            .ToListAsync();

        var stockByProduct = await _context.StockBalances
            .Where(sb => productIds.Contains(sb.Unit.ProductId) && sb.Unit.IsActive && sb.Unit.Status == ItemStatus.Active)
            .Where(sb => !branchWarehouseId.HasValue || sb.WarehouseId == branchWarehouseId.Value)
            .GroupBy(sb => sb.Unit.ProductId)
            .Select(g => new { ProductId = g.Key, Total = g.Sum(sb => sb.AvailableQuantity) })
            .ToListAsync();

        // Per-unit available stock (scoped to warehouse when specified) so the POS can flag
        // individual units as low-stock even when the product total is healthy.
        var stockByUnit = await _context.StockBalances
            .Where(sb => productIds.Contains(sb.Unit.ProductId))
            .Where(sb => !branchWarehouseId.HasValue || sb.WarehouseId == branchWarehouseId.Value)
            .GroupBy(sb => sb.UnitId)
            .Select(g => new { UnitId = g.Key, Total = g.Sum(sb => sb.AvailableQuantity) })
            .ToListAsync();

        var unitsByProduct = await _context.Units
            .Include(su => su.UnitOfMeasure)
            .AsNoTracking()
            .Where(su => productIds.Contains(su.ProductId) && su.Status != ItemStatus.Rejected)
            .Where(su => su.UnitUnitTypes.Any(uut => uut.UnitType!.Code == "UT_SELLING"))
            .ToListAsync();

        var stockLookup = stockByProduct.ToDictionary(x => x.ProductId, x => x.Total);
        var unitStockLookup = stockByUnit.ToDictionary(x => x.UnitId, x => x.Total);
        var unitsLookup = unitsByProduct.GroupBy(u => u.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var imagesLookup = imagesByProduct.GroupBy(m => m.EntityId).ToDictionary(g => g.Key, g => g.ToList());

        var items = new List<ProductDto>();
        foreach (var product in products)
        {
            var images = imagesLookup.GetValueOrDefault(product.Id);
            var mainImage = images?.FirstOrDefault(m => m.IsMain) ?? images?.FirstOrDefault();
            var imageUrl = mainImage != null ? _mediaService.GetMediaUrl(mainImage) : null;

            var dto = new ProductDto
            {
                Id = product.Id,
                NameEn = product.NameEn,
                NameAr = product.NameAr,
                DescriptionEn = product.DescriptionEn,
                DescriptionAr = product.DescriptionAr,
                Code = product.Code,
                TotalStock = stockLookup.GetValueOrDefault(product.Id, 0),
                CategoryId = product.CategoryId,
                CategoryNameEn = product.Category?.NameEn,
                CategoryNameAr = product.Category?.NameAr,
                ImageUrl = imageUrl,
                Status = product.Status,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };

            var productUnits = unitsLookup.GetValueOrDefault(product.Id);
            dto.Units = productUnits?.Select(su => new ProductUnitDto
            {
                Id = su.Id,
                UnitOfMeasureId = su.UnitOfMeasureId,
                UnitOfMeasureNameEn = su.UnitOfMeasure?.NameEn,
                UnitOfMeasureNameAr = su.UnitOfMeasure?.NameAr,
                Barcode = su.Barcode,
                Quantity = su.Quantity,
                LowStockThreshold = su.LowStockThreshold,
                SellingPrice = su.SellingPrice,
                IsActive = su.IsActive,
                AvailableQuantity = unitStockLookup.GetValueOrDefault(su.Id, 0)
            }).ToList() ?? new List<ProductUnitDto>();

            items.Add(dto);
        }

        return new PaginatedList<ProductDto>(items, count, pageNumber, pageSize);
    }

    public async Task<ProductDetailDto?> GetProductByIdAsync(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return null;

        var dto = await MapToDetailDtoAsync(product);
        return dto;
    }

    public async Task<ProductDto?> GetProductByCodeAsync(string code)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Code == code);

        return product == null ? null : await MapToDtoAsync(product);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            DescriptionEn = request.DescriptionEn,
            DescriptionAr = request.DescriptionAr,
            Code = request.Code,
            CategoryId = request.CategoryId,
            Status = Domain.Enums.ItemStatus.Draft,
            IsActive = false, // Draft products are never active — activation happens on request approval
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);

        // Auto-create add request so it appears in the Requests module
        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        var category = product.CategoryId.HasValue
            ? await _context.Categories.FindAsync(product.CategoryId.Value)
            : null;
        var newProductData = new
        {
            product.NameEn,
            product.NameAr,
            product.DescriptionEn,
            product.DescriptionAr,
            product.Code,
            product.CategoryId,
            CategoryEn = category?.NameEn,
            CategoryAr = category?.NameAr,
            Status = (request.Status.HasValue ? request.Status.Value : product.Status).ToString()
        };
        var addRequest = new Domain.Entities.Request
        {
            Id = Guid.NewGuid(),
            Type = Domain.Enums.RequestType.AddProduct,
            Status = Domain.Enums.RequestStatus.Pending,
            RequestedById = currentUserId,
            RequestedByName = currentUserName,
            ProductId = product.Id,
            ProductName = product.NameEn,
            NewDataJson = JsonSerializer.Serialize(newProductData),
            CreatedAt = DateTime.UtcNow
        };
        _context.Requests.Add(addRequest);

        await _context.SaveChangesAsync();

        // Log action
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Created,
                EntityName = "Product",
                EntityId = product.Id.ToString(),
                Details = null
            });

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

        return await MapToDtoAsync(product);
    }

    public async Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
            throw new KeyNotFoundException($"Product with ID {id} not found.");

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();

        // Draft products: update entity directly and refresh the pending AddProduct request
        if (product.Status == Domain.Enums.ItemStatus.Draft)
        {
            product.NameEn = request.NameEn;
            product.NameAr = request.NameAr;
            product.DescriptionEn = request.DescriptionEn;
            product.DescriptionAr = request.DescriptionAr;
            product.Code = request.Code;
            product.CategoryId = request.CategoryId;
            // Draft products stay inactive — intended status is stored in the request's NewDataJson
            // and applied when the request is approved
            product.UpdatedAt = DateTime.UtcNow;

            // Update the pending AddProduct request's NewDataJson
            var pendingRequest = await _context.Requests
                .Where(r => r.ProductId == product.Id
                         && r.Type == Domain.Enums.RequestType.AddProduct
                         && r.Status == Domain.Enums.RequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            if (pendingRequest != null)
            {
                var draftCategory = request.CategoryId.HasValue
                    ? await _context.Categories.FindAsync(request.CategoryId.Value)
                    : null;
                var intendedStatus = request.Status.HasValue ? request.Status.Value : product.Status;
                var updatedData = new
                {
                    NameEn = request.NameEn,
                    NameAr = request.NameAr,
                    DescriptionEn = request.DescriptionEn,
                    DescriptionAr = request.DescriptionAr,
                    Code = request.Code,
                    CategoryId = request.CategoryId,
                    CategoryEn = draftCategory?.NameEn,
                    CategoryAr = draftCategory?.NameAr,
                    Status = intendedStatus.ToString()
                };
                pendingRequest.NewDataJson = JsonSerializer.Serialize(updatedData);
                pendingRequest.ProductName = request.NameEn;
            }

            await _context.SaveChangesAsync();

            if (currentUserId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = currentUserId,
                    UserName = currentUserName,
                    Action = AuditAction.UpdatedDraft,
                    EntityName = "Product",
                    EntityId = product.Id.ToString(),
                    Details = null
                });

                if (pendingRequest != null)
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
            }

            return await MapToDtoAsync(product);
        }

        // Capture current state as old data
        var oldCategory = product.Category; // already included
        var oldProductData = new
        {
            product.NameEn,
            product.NameAr,
            product.DescriptionEn,
            product.DescriptionAr,
            product.Code,
            product.CategoryId,
            CategoryEn = oldCategory?.NameEn,
            CategoryAr = oldCategory?.NameAr,
            Status = product.Status.ToString()
        };

        var oldImageSnapshot = await _context.Media
            .Where(m => m.EntityId == product.Id && m.EntityType == EntityType.Product && m.CollectionName == "image")
            .OrderBy(m => m.Order)
            .ThenBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.FileName,
                m.Path,
                m.IsMain
            })
            .ToListAsync();

        var oldProductDataForRequest = new
        {
            oldProductData.NameEn,
            oldProductData.NameAr,
            oldProductData.DescriptionEn,
            oldProductData.DescriptionAr,
            oldProductData.Code,
            oldProductData.CategoryId,
            oldProductData.CategoryEn,
            oldProductData.CategoryAr,
            oldProductData.Status,
            Images = oldImageSnapshot
        };

        var newCategory = request.CategoryId.HasValue
            ? await _context.Categories.FindAsync(request.CategoryId.Value)
            : null;
        var newStatus = request.Status.HasValue ? request.Status.Value : product.Status;
        var baseNewProductData = new
        {
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            DescriptionEn = request.DescriptionEn,
            DescriptionAr = request.DescriptionAr,
            Code = request.Code,
            CategoryId = request.CategoryId,
            CategoryEn = newCategory?.NameEn,
            CategoryAr = newCategory?.NameAr,
            Status = newStatus.ToString()
        };

        var oldJson = JsonSerializer.Serialize(oldProductData);
        var newJson = JsonSerializer.Serialize(baseNewProductData);
        var removedImageIds = request.RemovedImageIds
            .Where(imageId => imageId != Guid.Empty)
            .Distinct()
            .ToList();
        var hasImageChanges = removedImageIds.Count > 0 || request.MainImageId.HasValue;

        // Skip request creation if nothing actually changed
        if (oldJson == newJson && !hasImageChanges)
        {
            return await MapToDtoAsync(product);
        }

        var newProductData = new Dictionary<string, object?>
        {
            [nameof(UpdateProductRequest.NameEn)] = request.NameEn,
            [nameof(UpdateProductRequest.NameAr)] = request.NameAr,
            [nameof(UpdateProductRequest.DescriptionEn)] = request.DescriptionEn,
            [nameof(UpdateProductRequest.DescriptionAr)] = request.DescriptionAr,
            [nameof(UpdateProductRequest.Code)] = request.Code,
            [nameof(UpdateProductRequest.CategoryId)] = request.CategoryId,
            ["CategoryEn"] = newCategory?.NameEn,
            ["CategoryAr"] = newCategory?.NameAr,
            [nameof(UpdateProductRequest.Status)] = newStatus.ToString()
        };

        if (removedImageIds.Count > 0)
            newProductData[nameof(UpdateProductRequest.RemovedImageIds)] = removedImageIds;

        if (request.MainImageId.HasValue)
            newProductData[nameof(UpdateProductRequest.MainImageId)] = request.MainImageId.Value;

        var pendingUpdateRequest = await _context.Requests
            .Where(r => r.ProductId == product.Id
                     && r.Type == Domain.Enums.RequestType.UpdateProduct
                     && r.Status == Domain.Enums.RequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        Domain.Entities.Request? requestEntityForAudit = null;
        var requestAuditAction = AuditAction.RequestedUpdate;

        if (pendingUpdateRequest != null)
        {
            pendingUpdateRequest.ProductName = request.NameEn;
            pendingUpdateRequest.NewDataJson = JsonSerializer.Serialize(newProductData);
            pendingUpdateRequest.UpdatedAt = DateTime.UtcNow;
            requestEntityForAudit = pendingUpdateRequest;
            requestAuditAction = AuditAction.UpdatedRequest;
        }
        else
        {
            // Auto-create update request — changes will be applied when approved
            var createdUpdateRequest = new Domain.Entities.Request
            {
                Id = Guid.NewGuid(),
                Type = Domain.Enums.RequestType.UpdateProduct,
                Status = Domain.Enums.RequestStatus.Pending,
                RequestedById = currentUserId,
                RequestedByName = currentUserName,
                ProductId = product.Id,
                ProductName = request.NameEn,
                OldDataJson = JsonSerializer.Serialize(oldProductDataForRequest),
                NewDataJson = JsonSerializer.Serialize(newProductData),
                CreatedAt = DateTime.UtcNow
            };

            _context.Requests.Add(createdUpdateRequest);
            requestEntityForAudit = createdUpdateRequest;
        }

        await _context.SaveChangesAsync();

        // Log action
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.RequestedUpdate,
                EntityName = "Product",
                EntityId = product.Id.ToString(),
                Details = null
            });

            if (requestEntityForAudit != null)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = currentUserId,
                    UserName = currentUserName,
                    Action = requestAuditAction,
                    EntityName = "Request",
                    EntityId = requestEntityForAudit.Id.ToString(),
                    Details = $"{requestEntityForAudit.Type}: {requestEntityForAudit.ProductName} ({requestEntityForAudit.Status})"
                });
            }
        }

        return await MapToDtoAsync(product);
    }

    public async Task DeleteProductAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);

        if (product == null)
        {
            throw new KeyNotFoundException($"Product with ID {id} not found.");
        }

        if (await _context.Units.AnyAsync(u => u.ProductId == id))
        {
            throw new InvalidOperationException("Cannot delete product: it has selling units. Delete the units first.");
        }

        // Remove any pending requests related to this product
        var pendingRequests = await _context.Requests
            .Where(r => r.Status == RequestStatus.Pending && r.ProductId == id)
            .ToListAsync();
        if (pendingRequests.Any())
            _context.Requests.RemoveRange(pendingRequests);

        // Delete associated media
        var mediaList = await _mediaService.GetMediaForEntityAsync(id, EntityType.Product);
        foreach (var media in mediaList)
        {
            await _mediaService.DeleteMediaAsync(media.Id);
        }

        _context.Products.Remove(product);
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
                EntityName = "Product",
                EntityId = product.Id.ToString(),
                Details = null
            });
        }
    }

    public async Task<bool> CheckCodeExistsAsync(string code, Guid? excludeProductId = null)
    {
        var query = _context.Products.AsQueryable();

        if (excludeProductId.HasValue)
        {
            query = query.Where(p => p.Id != excludeProductId.Value);
        }

        return await query.AnyAsync(p => p.Code == code);
    }

    private async Task<string?> GetProductImageUrlAsync(Guid productId)
    {
        var mediaList = await _mediaService.GetMediaForEntityAsync(productId, EntityType.Product, "image");
        // Get the main image first, or fall back to the first image
        var image = mediaList.FirstOrDefault(m => m.IsMain) ?? mediaList.FirstOrDefault();
        return image != null ? _mediaService.GetMediaUrl(image) : null;
    }

    private async Task<ProductDto> MapToDtoAsync(Product product, Guid? warehouseId = null)
    {
        var imageUrl = await GetProductImageUrlAsync(product.Id);

        // Combine stock, sold, and refunded into a single DB round-trip
        var stockQuery = _context.StockBalances
            .Where(sb => sb.Unit.ProductId == product.Id && sb.Unit.IsActive);
        if (warehouseId.HasValue)
            stockQuery = stockQuery.Where(sb => sb.WarehouseId == warehouseId.Value);

        var aggregates = await _context.Products
            .Where(p => p.Id == product.Id)
            .Select(p => new
            {
                TotalStock = stockQuery.Sum(sb => (int?)sb.AvailableQuantity) ?? 0
            })
            .FirstOrDefaultAsync();

        // Load selling units with projection (avoids Include overhead)
        var units = await _context.Units
            .Where(su => su.ProductId == product.Id && su.IsActive)
            .Where(su => su.UnitUnitTypes.Any(uut => uut.UnitType!.Code == "UT_SELLING"))
            .Select(su => new ProductUnitDto
            {
                Id = su.Id,
                UnitOfMeasureId = su.UnitOfMeasureId,
                UnitOfMeasureNameEn = su.UnitOfMeasure != null ? su.UnitOfMeasure.NameEn : null,
                UnitOfMeasureNameAr = su.UnitOfMeasure != null ? su.UnitOfMeasure.NameAr : null,
                Barcode = su.Barcode,
                Quantity = su.Quantity,
                LowStockThreshold = su.LowStockThreshold,
                SellingPrice = su.SellingPrice,
                IsActive = su.IsActive,
                AvailableQuantity = _context.StockBalances
                    .Where(sb => sb.UnitId == su.Id)
                    .Where(sb => !warehouseId.HasValue || sb.WarehouseId == warehouseId.Value)
                    .Sum(sb => (int?)sb.AvailableQuantity) ?? 0
            })
            .ToListAsync();

        return new ProductDto
        {
            Id = product.Id,
            NameEn = product.NameEn,
            NameAr = product.NameAr,
            DescriptionEn = product.DescriptionEn,
            DescriptionAr = product.DescriptionAr,
            Code = product.Code,
            TotalStock = aggregates?.TotalStock ?? 0,
            CategoryId = product.CategoryId,
            CategoryNameEn = product.Category?.NameEn,
            CategoryNameAr = product.Category?.NameAr,
            ImageUrl = imageUrl,
            Status = product.Status,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Units = units
        };
    }

    private async Task<ProductDetailDto> MapToDetailDtoAsync(Product product)
    {
        var baseDto = await MapToDtoAsync(product);

        var detailDto = new ProductDetailDto();
        // Copy base properties
        foreach (var prop in typeof(ProductDto).GetProperties())
        {
            if (prop.CanWrite)
                prop.SetValue(detailDto, prop.GetValue(baseDto));
        }

        // Stock by location (using projection instead of Include)
        detailDto.StockByLocation = await _context.StockBalances
            .Where(sb => sb.Unit.ProductId == product.Id)
            .Select(sb => new ProductStockDetailDto
            {
                WarehouseId = sb.WarehouseId,
                WarehouseNameEn = sb.Warehouse.NameEn,
                WarehouseNameAr = sb.Warehouse.NameAr,
                WarehouseType = sb.Warehouse.WarehouseType != null ? sb.Warehouse.WarehouseType.NameEn : string.Empty,
                AvailableQuantity = sb.AvailableQuantity,
                ReservedQuantity = sb.ReservedQuantity,
                InTransitQuantity = sb.InTransitQuantity
            })
            .ToListAsync();

        // Recent stock movements (last 20, using projection)
        detailDto.RecentStockMovements = await _context.InventoryHistories
            .Where(ih => ih.Unit.ProductId == product.Id)
            .OrderByDescending(ih => ih.PerformedAt)
            .Take(20)
            .Select(ih => new ProductStockMovementDto
            {
                PerformedAt = ih.PerformedAt,
                WarehouseNameEn = ih.Warehouse.NameEn,
                WarehouseNameAr = ih.Warehouse.NameAr,
                ActionType = ih.ActionType.ToString(),
                QuantityChange = ih.QuantityChange,
                AvailableQuantityAfter = ih.AvailableQuantityAfter,
                PerformedBy = ih.PerformedBy
            })
            .ToListAsync();

        return detailDto;
    }

}
