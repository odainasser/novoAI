using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Requests;
using Application.Features.UserLogs;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class RequestService : IRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGoodsReceivingService _goodsReceivingService;
    private readonly IStockAdjustmentService _stockAdjustmentService;
    private readonly IStockTransferService _stockTransferService;
    private readonly IMediaService _mediaService;
    private readonly IUserLogService _userLogService;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RequestService> _logger;

    public RequestService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IGoodsReceivingService goodsReceivingService,
        IStockAdjustmentService stockAdjustmentService,
        IStockTransferService stockTransferService,
        IMediaService mediaService,
        IUserLogService userLogService,
        IEmailService emailService,
        INotificationService notificationService,
        ILogger<RequestService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _goodsReceivingService = goodsReceivingService;
        _stockAdjustmentService = stockAdjustmentService;
        _stockTransferService = stockTransferService;
        _mediaService = mediaService;
        _userLogService = userLogService;
        _emailService = emailService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<PaginatedList<RequestDto>> GetAllRequestsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        RequestType? type = null,
        RequestStatus? status = null)
    {
        var query = _context.Requests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r =>
                (r.ProductName != null && r.ProductName.ToLower().Contains(s)) ||
                r.RequestedByName.ToLower().Contains(s));
        }

        if (type.HasValue)
            query = query.Where(r => r.Type == type.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        // Order by most recent activity: approval, rejection, update, or creation
        query = query.OrderByDescending(r =>
            r.ApprovedAt.HasValue && r.RejectedAt.HasValue
                ? (r.ApprovedAt.Value > r.RejectedAt.Value ? r.ApprovedAt.Value : r.RejectedAt.Value)
                : r.ApprovedAt ?? r.RejectedAt ?? r.UpdatedAt ?? r.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedList<RequestDto>(dtos, count, pageNumber, pageSize);
    }

    public async Task<PaginatedList<RequestDto>> GetByRequesterIdsAsync(
        IEnumerable<Guid> requesterIds,
        int pageNumber,
        int pageSize,
        RequestStatus? status = null)
    {
        var ids = requesterIds?.ToList() ?? new List<Guid>();
        if (ids.Count == 0)
        {
            return new PaginatedList<RequestDto>(new List<RequestDto>(), 0, pageNumber, pageSize);
        }

        var query = _context.Requests.Where(r => ids.Contains(r.RequestedById));

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        query = query.OrderByDescending(r => r.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedList<RequestDto>(items.Select(MapToDto).ToList(), count, pageNumber, pageSize);
    }

    public async Task<RequestDto?> GetRequestByIdAsync(Guid id)
    {
        var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<RequestDto?> GetPendingProductUpdateRequestAsync(Guid productId)
    {
        var entity = await _context.Requests
            .Where(r => r.ProductId == productId
                     && r.Type == RequestType.UpdateProduct
                     && r.Status == RequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<RequestDto> CreateSetUnitPriceRequestAsync(CreateSetUnitPriceRequest request)
    {
        var unit = await _context.Units
            .Include(s => s.Product)
            .Include(s => s.UnitOfMeasure)
            .FirstOrDefaultAsync(s => s.Id == request.UnitId);
        if (unit == null)
            throw new ArgumentException("Unit not found");

        if (unit.Status != ItemStatus.Active)
            throw new InvalidOperationException("Cannot request price change for a unit that is not approved (active).");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var displayName = $"{unit.Product?.NameEn} ({unit.UnitOfMeasure?.NameEn})";

        var effectiveSellingBarcode = string.IsNullOrWhiteSpace(request.SellingBarcode)
            ? (unit.SellingBarcode ?? string.Empty)
            : request.SellingBarcode.Trim();

        var oldData = new
        {
            SellingPrice = unit.SellingPrice,
            SellingBarcode = unit.SellingBarcode ?? string.Empty,
            LowStockThreshold = unit.LowStockThreshold
        };

        var newData = new
        {
            SellingPrice = request.NewPrice,
            SellingBarcode = effectiveSellingBarcode,
            LowStockThreshold = request.LowStockThreshold
        };

        // Update existing pending request instead of creating duplicates.
        var existing = await _context.Requests.FirstOrDefaultAsync(r =>
            r.Type == RequestType.SetUnitPrice &&
            r.UnitId == request.UnitId &&
            r.Status == RequestStatus.Pending);

        if (existing != null)
        {
            existing.CurrentPrice = unit.SellingPrice;
            existing.NewPrice = request.NewPrice;
            existing.NewDataJson = JsonSerializer.Serialize(newData);
            existing.OldDataJson = JsonSerializer.Serialize(oldData);
            existing.Note = request.Note;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogRequestAsync(existing, AuditAction.UpdatedRequest);
            return MapToDto(existing);
        }

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.SetUnitPrice,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            UnitId = unit.Id,
            ProductName = displayName,
            CurrentPrice = unit.SellingPrice,
            NewPrice = request.NewPrice,
            OldDataJson = JsonSerializer.Serialize(oldData),
            NewDataJson = JsonSerializer.Serialize(newData),
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedSetSellingDetails);

        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateActivateProductRequestAsync(CreateActivateProductRequest request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product == null)
            throw new ArgumentException("Product not found");

        if (product.Status == ItemStatus.Active)
            throw new InvalidOperationException("Product is already active");

        // Prevent duplicate pending requests for the same product
        var existing = await _context.Requests.AnyAsync(r =>
            r.Type == RequestType.ActivateProduct &&
            r.ProductId == request.ProductId &&
            r.Status == RequestStatus.Pending);
        if (existing)
            throw new InvalidOperationException("A pending activation request already exists for this product");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.ActivateProduct,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            ProductId = product.Id,
            ProductName = product.NameEn,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedActivation);

        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateActivateUnitRequestAsync(CreateActivateUnitRequest request)
    {
        var unit = await _context.Units
            .Include(u => u.Product)
            .Include(u => u.UnitOfMeasure)
            .FirstOrDefaultAsync(u => u.Id == request.UnitId);
        if (unit == null)
            throw new ArgumentException("Unit not found");

        if (unit.Status == ItemStatus.Active)
            throw new InvalidOperationException("Unit is already active");

        var existing = await _context.Requests.AnyAsync(r =>
            r.Type == RequestType.ActivateUnit &&
            r.UnitId == request.UnitId &&
            r.Status == RequestStatus.Pending);
        if (existing)
            throw new InvalidOperationException("A pending activation request already exists for this unit");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var displayName = $"{unit.Product?.NameEn} ({unit.UnitOfMeasure?.NameEn})";

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.ActivateUnit,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            UnitId = unit.Id,
            ProductName = displayName,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedActivation);

        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateAddGRNRequestAsync(CreateInventoryGRNRequest request)
    {
        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var json = JsonSerializer.Serialize(request.Data);
        var label = $"GRN - {request.Data.ReceivedDate?.ToString("dd/MM/yyyy") ?? DateTime.Today.ToString("dd/MM/yyyy")}";

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.AddGRN,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            ProductName = label,
            NewDataJson = json,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedUpdate);
        return MapToDto(entity);
    }

    public async Task<RequestDto?> UpdateAddGRNRequestAsync(Guid id, CreateInventoryGRNRequest request)
    {
        var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null)
            return null;

        if (entity.Type != RequestType.AddGRN)
            throw new InvalidOperationException("Request type mismatch. Expected AddGRN.");

        if (entity.Status != RequestStatus.Pending)
            throw new InvalidOperationException("Only pending GRN requests can be updated.");

        var json = JsonSerializer.Serialize(request.Data);
        var label = $"GRN - {request.Data.ReceivedDate?.ToString("dd/MM/yyyy") ?? DateTime.Today.ToString("dd/MM/yyyy")}";

        entity.ProductName = label;
        entity.NewDataJson = json;
        entity.Note = request.Note ?? entity.Note;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.UpdatedRequest);
        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateAddStockAdjustmentRequestAsync(CreateInventoryAdjustmentRequest request)
    {
        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var warehouse = await _context.Warehouses.FindAsync(request.Data.WarehouseId);
        var label = $"{request.Data.AdjustmentType} - {warehouse?.NameEn ?? request.Data.WarehouseId.ToString()}";

        var json = JsonSerializer.Serialize(request.Data);

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.AddStockAdjustment,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            ProductName = label,
            NewDataJson = json,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedUpdate);
        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateAddStockTransferRequestAsync(CreateInventoryTransferRequest request)
    {
        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var warehouse = await _context.Warehouses.FindAsync(request.Data.WarehouseId);
        var label = $"{request.Data.TransferType} - {warehouse?.NameEn ?? request.Data.WarehouseId.ToString()}";

        var json = JsonSerializer.Serialize(request.Data);

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.AddStockTransfer,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            ProductName = label,
            NewDataJson = json,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedUpdate);
        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateDeleteProductRequestAsync(CreateDeleteProductRequest request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product == null)
            throw new ArgumentException("Product not found");

        // Prevent duplicate pending delete requests for the same product
        var existing = await _context.Requests.AnyAsync(r =>
            r.Type == RequestType.DeleteProduct &&
            r.ProductId == request.ProductId &&
            r.Status == RequestStatus.Pending);
        if (existing)
            throw new InvalidOperationException("A pending delete request already exists for this product");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.DeleteProduct,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            ProductId = product.Id,
            ProductName = product.NameEn,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedDeletion);

        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateDeleteUnitRequestAsync(CreateDeleteUnitRequest request)
    {
        var unit = await _context.Units
            .Include(u => u.Product)
            .Include(u => u.UnitOfMeasure)
            .FirstOrDefaultAsync(u => u.Id == request.UnitId);
        if (unit == null)
            throw new ArgumentException("Unit not found");

        // Prevent duplicate pending delete requests for the same unit
        var existing = await _context.Requests.AnyAsync(r =>
            r.Type == RequestType.DeleteUnit &&
            r.UnitId == request.UnitId &&
            r.Status == RequestStatus.Pending);
        if (existing)
            throw new InvalidOperationException("A pending delete request already exists for this unit");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var displayName = $"{unit.Product?.NameEn} ({unit.UnitOfMeasure?.NameEn})";

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.DeleteUnit,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            UnitId = unit.Id,
            ProductName = displayName,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedDeletion);

        return MapToDto(entity);
    }

    public async Task<RequestDto> CreateSetLogisticsDetailsRequestAsync(CreateSetLogisticsDetailsRequest request)
    {
        var unit = await _context.Units
            .Include(u => u.Product)
            .Include(u => u.UnitOfMeasure)
            .Include(u => u.UnitSuppliers).ThenInclude(us => us.Supplier)
            .FirstOrDefaultAsync(u => u.Id == request.UnitId);
        if (unit == null)
            throw new ArgumentException("Unit not found");

        if (unit.Status != ItemStatus.Active)
            throw new InvalidOperationException("Cannot request logistics change for a unit that is not approved (active).");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        var displayName = $"{unit.Product?.NameEn} ({unit.UnitOfMeasure?.NameEn})";

        // Resolve supplier names for the new data
        var supplierIds = request.Suppliers.Select(s => s.SupplierId).Distinct().ToList();
        var suppliers = await _context.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        var newData = new
        {
            Cost = request.NewCost,
            LowStockThreshold = request.LowStockThreshold,
            Suppliers = request.Suppliers.Select(s => new
            {
                s.SupplierId,
                SupplierNameEn = suppliers.TryGetValue(s.SupplierId, out var sup) ? sup.NameEn : string.Empty,
                SupplierNameAr = suppliers.TryGetValue(s.SupplierId, out var supAr) ? supAr.NameAr : string.Empty,
                s.Barcode
            }).ToList()
        };

        var oldData = new
        {
            Cost = unit.Cost,
            LowStockThreshold = unit.LowStockThreshold,
            Suppliers = unit.UnitSuppliers.Select(s => new
            {
                s.SupplierId,
                SupplierNameEn = s.Supplier?.NameEn ?? string.Empty,
                SupplierNameAr = s.Supplier?.NameAr ?? string.Empty,
                s.Barcode
            }).ToList()
        };

        // Check for an existing pending request — update it instead of creating a new one
        var existing = await _context.Requests.FirstOrDefaultAsync(r =>
            r.Type == RequestType.SetLogisticsDetails &&
            r.UnitId == request.UnitId &&
            r.Status == RequestStatus.Pending);

        if (existing != null)
        {
            existing.NewPrice = request.NewCost;
            existing.NewDataJson = JsonSerializer.Serialize(newData);
            existing.OldDataJson = JsonSerializer.Serialize(oldData);
            existing.Note = request.Note;
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await LogRequestAsync(existing, AuditAction.UpdatedRequest);
            return MapToDto(existing);
        }

        var entity = new Request
        {
            Id = Guid.NewGuid(),
            Type = RequestType.SetLogisticsDetails,
            Status = RequestStatus.Pending,
            RequestedById = userId,
            RequestedByName = userName,
            UnitId = unit.Id,
            ProductName = displayName,
            CurrentPrice = unit.Cost,
            NewPrice = request.NewCost,
            NewDataJson = JsonSerializer.Serialize(newData),
            OldDataJson = JsonSerializer.Serialize(oldData),
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(entity);
        await _context.SaveChangesAsync();
        await LogRequestAsync(entity, AuditAction.RequestedSetLogisticsDetails);

        return MapToDto(entity);
    }

    public async Task<RequestDto?> GetPendingSetLogisticsDetailsRequestAsync(Guid unitId)
    {
        var entity = await _context.Requests.FirstOrDefaultAsync(r =>
            r.Type == RequestType.SetLogisticsDetails &&
            r.UnitId == unitId &&
            r.Status == RequestStatus.Pending);
        return entity == null ? null : MapToDto(entity);
    }

    public async Task<RequestDto?> ReviewRequestAsync(Guid id, ReviewRequestDto review)
    {
        RequestDto? result = null;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
        var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null) { result = null; return; }

        if (entity.Status != RequestStatus.Pending)
            throw new InvalidOperationException("Request has already been reviewed");

        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();

        entity.ReviewNote = review.ReviewNote;

        if (review.Approve)
        {
            entity.Status = RequestStatus.Approved;
            entity.ApprovedById = userId;
            entity.ApprovedByName = userName;
            entity.ApprovedAt = DateTime.UtcNow;

            // Apply price change
            if (entity.Type == RequestType.SetUnitPrice && entity.UnitId.HasValue)
            {
                var unit = await _context.Units.FindAsync(entity.UnitId.Value);
                if (unit != null && entity.NewPrice.HasValue)
                {
                    unit.SellingPrice = entity.NewPrice.Value;
                    if (!string.IsNullOrEmpty(entity.NewDataJson))
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(entity.NewDataJson);
                        if (data.TryGetProperty("SellingBarcode", out var barcodeEl))
                            unit.SellingBarcode = barcodeEl.GetString() ?? string.Empty;
                        if (data.TryGetProperty("LowStockThreshold", out var thresholdEl))
                            unit.LowStockThreshold = thresholdEl.GetInt32();
                    }
                    unit.UpdatedAt = DateTime.UtcNow;
                }
            }
            // Apply logistics change
            else if (entity.Type == RequestType.SetLogisticsDetails && entity.UnitId.HasValue)
            {
                var unit = await _context.Units
                    .Include(u => u.UnitSuppliers)
                    .FirstOrDefaultAsync(u => u.Id == entity.UnitId.Value);
                if (unit != null && !string.IsNullOrEmpty(entity.NewDataJson))
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(entity.NewDataJson);
                    if (data.TryGetProperty("Cost", out var costEl))
                        unit.Cost = costEl.GetDecimal();

                    if (data.TryGetProperty("LowStockThreshold", out var thresholdEl))
                        unit.LowStockThreshold = thresholdEl.GetInt32();

                    if (data.TryGetProperty("Suppliers", out var suppliersEl))
                    {
                        _context.UnitSuppliers.RemoveRange(unit.UnitSuppliers);
                        foreach (var s in suppliersEl.EnumerateArray())
                        {
                            var supplierId = s.GetProperty("SupplierId").GetGuid();
                            var barcode = s.GetProperty("Barcode").GetString() ?? string.Empty;
                            _context.UnitSuppliers.Add(new UnitSupplier
                            {
                                UnitId = unit.Id,
                                SupplierId = supplierId,
                                Barcode = barcode
                            });
                        }
                    }
                    unit.UpdatedAt = DateTime.UtcNow;
                }
            }
            // Activate product
            else if (entity.Type == RequestType.ActivateProduct && entity.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(entity.ProductId.Value);
                if (product != null)
                {
                    product.Status = ItemStatus.Active;
                    product.IsActive = true;
                    product.UpdatedAt = DateTime.UtcNow;
                }
                await TransferMediaToProductAsync(entity.Id, entity.ProductId.Value);
            }
            // Activate unit
            else if (entity.Type == RequestType.ActivateUnit && entity.UnitId.HasValue)
            {
                var unit = await _context.Units.FindAsync(entity.UnitId.Value);
                if (unit != null)
                {
                    unit.Status = ItemStatus.Active;
                    unit.IsActive = true;
                    unit.UpdatedAt = DateTime.UtcNow;
                }
            }
            // Add product — apply intended status and transfer gallery
            else if (entity.Type == RequestType.AddProduct && entity.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(entity.ProductId.Value);
                if (product != null)
                {
                    var intendedStatus = ItemStatus.Active;
                    if (!string.IsNullOrEmpty(entity.NewDataJson))
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(entity.NewDataJson);
                        if (data.TryGetProperty("Status", out var statusProp) &&
                            Enum.TryParse<ItemStatus>(statusProp.GetString(), out var parsed) &&
                            parsed != ItemStatus.Draft)
                        {
                            intendedStatus = parsed;
                        }
                    }
                    product.Status = intendedStatus;
                    product.IsActive = intendedStatus == ItemStatus.Active;
                    product.UpdatedAt = DateTime.UtcNow;
                }
                await TransferMediaToProductAsync(entity.Id, entity.ProductId.Value);
            }
            // Update product — apply deferred new data and transfer gallery
            else if (entity.Type == RequestType.UpdateProduct && entity.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(entity.ProductId.Value);
                if (product != null && !string.IsNullOrEmpty(entity.NewDataJson))
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(entity.NewDataJson);
                    if (data.TryGetProperty("NameEn", out var nameEn)) product.NameEn = nameEn.GetString() ?? product.NameEn;
                    if (data.TryGetProperty("NameAr", out var nameAr)) product.NameAr = nameAr.GetString() ?? product.NameAr;
                    if (data.TryGetProperty("DescriptionEn", out var descEn)) product.DescriptionEn = descEn.ValueKind == JsonValueKind.Null ? null : descEn.GetString();
                    if (data.TryGetProperty("DescriptionAr", out var descAr)) product.DescriptionAr = descAr.ValueKind == JsonValueKind.Null ? null : descAr.GetString();
                    if (data.TryGetProperty("Code", out var code)) product.Code = code.GetString() ?? product.Code;
                    if (data.TryGetProperty("CategoryId", out var catId))
                        product.CategoryId = catId.ValueKind == JsonValueKind.Null ? null : catId.GetGuid();
                    if (data.TryGetProperty("Status", out var statusProp) &&
                        Enum.TryParse<ItemStatus>(statusProp.GetString(), out var parsedStatus))
                    {
                        product.Status = parsedStatus;
                        product.IsActive = parsedStatus == ItemStatus.Active;
                    }

                    var removedImageIds = GetGuidArray(data, "RemovedImageIds");
                    foreach (var removedImageId in removedImageIds)
                    {
                        var productMedia = await _context.Media.FirstOrDefaultAsync(m =>
                            m.Id == removedImageId &&
                            m.EntityId == product.Id &&
                            m.EntityType == EntityType.Product);

                        if (productMedia != null)
                            await _mediaService.DeleteMediaAsync(productMedia.Id);
                    }

                    product.UpdatedAt = DateTime.UtcNow;

                    await TransferMediaToProductAsync(entity.Id, entity.ProductId.Value);

                    await NormalizeProductMainImageAsync(product.Id);

                    if (data.TryGetProperty("MainImageId", out var mainImageIdProp) &&
                        mainImageIdProp.ValueKind != JsonValueKind.Null)
                    {
                        var mainImageId = mainImageIdProp.GetGuid();
                        var selectedMain = await _context.Media.FirstOrDefaultAsync(m =>
                            m.Id == mainImageId &&
                            m.EntityId == product.Id &&
                            m.EntityType == EntityType.Product &&
                            m.CollectionName == "image");

                        if (selectedMain != null)
                            await _mediaService.SetMainMediaAsync(selectedMain.Id, product.Id, EntityType.Product, "image");

                        await NormalizeProductMainImageAsync(product.Id, selectedMain?.Id);
                    }
                    else
                    {
                        await NormalizeProductMainImageAsync(product.Id);
                    }
                }
            }
            // Add unit — apply intended status
            else if (entity.Type == RequestType.AddUnit && entity.UnitId.HasValue)
            {
                var unit = await _context.Units.FindAsync(entity.UnitId.Value);
                if (unit != null)
                {
                    var intendedStatus = ItemStatus.Active;
                    if (!string.IsNullOrEmpty(entity.NewDataJson))
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(entity.NewDataJson);
                        if (data.TryGetProperty("Status", out var statusProp) &&
                            Enum.TryParse<ItemStatus>(statusProp.GetString(), out var parsed) &&
                            parsed != ItemStatus.Draft)
                        {
                            intendedStatus = parsed;
                        }
                    }
                    unit.Status = intendedStatus;
                    unit.IsActive = intendedStatus == ItemStatus.Active;
                    unit.UpdatedAt = DateTime.UtcNow;
                }
            }
            // Update unit — apply deferred new data
            else if (entity.Type == RequestType.UpdateUnit && entity.UnitId.HasValue)
            {
                var unit = await _context.Units
                    .Include(u => u.UnitUnitTypes)
                    .FirstOrDefaultAsync(u => u.Id == entity.UnitId.Value);
                if (unit != null && !string.IsNullOrEmpty(entity.NewDataJson))
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(entity.NewDataJson);
                    if (data.TryGetProperty("UnitOfMeasureId", out var uomId)) unit.UnitOfMeasureId = uomId.GetGuid();
                    if (data.TryGetProperty("ProductId", out var prodId)) unit.ProductId = prodId.GetGuid();
                    if (data.TryGetProperty("Quantity", out var qty)) unit.Quantity = qty.GetInt32();
                    if (data.TryGetProperty("LowStockThreshold", out var unitThreshold)) unit.LowStockThreshold = unitThreshold.GetInt32();
                    // Replace unit type associations
                    if (data.TryGetProperty("UnitTypeIds", out var typeIds))
                    {
                        _context.Set<UnitUnitType>().RemoveRange(unit.UnitUnitTypes);
                        foreach (var el in typeIds.EnumerateArray())
                        {
                            _context.Set<UnitUnitType>().Add(new UnitUnitType
                            {
                                UnitId = unit.Id,
                                UnitTypeId = el.GetGuid()
                            });
                        }
                    }
                    if (data.TryGetProperty("Status", out var unitStatusProp) &&
                        Enum.TryParse<ItemStatus>(unitStatusProp.GetString(), out var parsedUnitStatus))
                    {
                        unit.Status = parsedUnitStatus;
                        unit.IsActive = parsedUnitStatus == ItemStatus.Active;
                    }
                    unit.UpdatedAt = DateTime.UtcNow;
                }
            }
            // Add GRN
            else if (entity.Type == RequestType.AddGRN && !string.IsNullOrEmpty(entity.NewDataJson))
            {
                var data = JsonSerializer.Deserialize<Application.Features.Inventory.CreateGoodsReceivingNoteRequest>(entity.NewDataJson);
                if (data != null)
                    await _goodsReceivingService.CreateAsync(data);
            }
            // Add stock adjustment
            else if (entity.Type == RequestType.AddStockAdjustment && !string.IsNullOrEmpty(entity.NewDataJson))
            {
                var data = JsonSerializer.Deserialize<Application.Features.Inventory.CreateStockAdjustmentRequest>(entity.NewDataJson);
                if (data != null)
                    await _stockAdjustmentService.CreateAsync(data);
            }
            // Add stock transfer
            else if (entity.Type == RequestType.AddStockTransfer && !string.IsNullOrEmpty(entity.NewDataJson))
            {
                var data = JsonSerializer.Deserialize<Application.Features.Inventory.CreateStockTransferRequest>(entity.NewDataJson);
                if (data != null)
                    await _stockTransferService.CreateAsync(data);
            }
            // Delete product — hard delete with media cleanup
            else if (entity.Type == RequestType.DeleteProduct && entity.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(entity.ProductId.Value);
                if (product != null)
                {
                    var mediaList = await _mediaService.GetMediaForEntityAsync(product.Id, EntityType.Product);
                    foreach (var media in mediaList)
                        await _mediaService.DeleteMediaAsync(media.Id);

                    _context.Products.Remove(product);
                }
            }
            // Delete unit — soft delete
            else if (entity.Type == RequestType.DeleteUnit && entity.UnitId.HasValue)
            {
                var unit = await _context.Units.FindAsync(entity.UnitId.Value);
                if (unit != null)
                {
                    unit.IsDeleted = true;
                    unit.DeletedAt = DateTime.UtcNow;
                }
            }
            // Approve purchase request — mark ready to convert (no stock moves here)
            else if (entity.Type == RequestType.AddPurchaseRequest && !string.IsNullOrEmpty(entity.NewDataJson))
            {
                var pr = await ResolveLinkedPurchaseRequestAsync(entity.NewDataJson);
                if (pr != null && pr.Status == PurchaseRequestStatus.Submitted)
                {
                    pr.Status = PurchaseRequestStatus.Approved;
                    pr.ApprovedById = userId;
                    pr.ApprovedByName = userName;
                    pr.ApprovedAt = DateTime.UtcNow;
                    pr.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
        else
        {
            entity.Status = RequestStatus.Rejected;
            entity.RejectedById = userId;
            entity.RejectedByName = userName;
            entity.RejectedAt = DateTime.UtcNow;

            // Only mark the entity as Rejected for Add/Activate types (new/pending entities).
            // Update requests are rejected silently — the existing entity keeps its current status.
            if ((entity.Type == RequestType.AddProduct || entity.Type == RequestType.ActivateProduct) && entity.ProductId.HasValue)
            {
                var product = await _context.Products.FindAsync(entity.ProductId.Value);
                if (product != null)
                {
                    product.Status = ItemStatus.Rejected;
                    product.IsActive = false;
                    product.UpdatedAt = DateTime.UtcNow;
                }
            }
            else if ((entity.Type == RequestType.AddUnit || entity.Type == RequestType.ActivateUnit) && entity.UnitId.HasValue)
            {
                var unit = await _context.Units.FindAsync(entity.UnitId.Value);
                if (unit != null)
                {
                    unit.Status = ItemStatus.Rejected;
                    unit.IsActive = false;
                    unit.UpdatedAt = DateTime.UtcNow;
                }
            }
            // Reject purchase request — record the reason on the PR
            else if (entity.Type == RequestType.AddPurchaseRequest && !string.IsNullOrEmpty(entity.NewDataJson))
            {
                var pr = await ResolveLinkedPurchaseRequestAsync(entity.NewDataJson);
                if (pr != null && pr.Status == PurchaseRequestStatus.Submitted)
                {
                    pr.Status = PurchaseRequestStatus.Rejected;
                    pr.RejectedById = userId;
                    pr.RejectedByName = userName;
                    pr.RejectedAt = DateTime.UtcNow;
                    pr.RejectReason = review.ReviewNote;
                    pr.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();

        // Log approval/rejection to the affected entity
        var reviewAction = review.Approve ? AuditAction.ApprovedRequest : AuditAction.RejectedRequest;
        await LogRequestAsync(entity, reviewAction);

        await transaction.CommitAsync();
        result = MapToDto(entity);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        });

        // Notify the requester by email (outside the transaction so a mail failure
        // never rolls back the approval/rejection). Failures are logged only.
        if (result != null)
        {
            await NotifyRequesterOfReviewAsync(id, review.Approve);
        }

        return result;
    }

    private async Task NotifyRequesterOfReviewAsync(Guid requestId, bool approved)
    {
        try
        {
            var notice = await (from r in _context.Requests
                                join u in _context.Set<ApplicationUser>() on r.RequestedById equals u.Id
                                where r.Id == requestId
                                select new
                                {
                                    UserId = u.Id,
                                    Email = u.Email,
                                    IsActive = u.IsActive,
                                    IsDeleted = u.IsDeleted,
                                    RequestedById = r.RequestedById,
                                    RequestedByName = r.RequestedByName,
                                    Type = r.Type,
                                    ReviewNote = r.ReviewNote,
                                    ProductName = r.ProductName,
                                    ApprovedByName = r.ApprovedByName,
                                    RejectedByName = r.RejectedByName,
                                    DecidedAt = approved ? r.ApprovedAt : r.RejectedAt
                                })
                              .FirstOrDefaultAsync();

            if (notice == null || !notice.IsActive || notice.IsDeleted)
            {
                return;
            }

            var reviewer = approved ? notice.ApprovedByName : notice.RejectedByName;

            // In-app real-time notification (always — even if user has no email)
            await PublishRequestReviewNotificationAsync(
                notice.RequestedById,
                notice.Type,
                approved,
                reviewer ?? string.Empty,
                notice.ProductName);

            // Email (only if user has an address)
            if (!string.IsNullOrWhiteSpace(notice.Email))
            {
                await _emailService.SendRequestActionAsync(
                    notice.Email!,
                    notice.RequestedByName,
                    notice.Type,
                    approved,
                    reviewer ?? string.Empty,
                    notice.ReviewNote,
                    notice.ProductName,
                    notice.DecidedAt ?? DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify requester of request review {RequestId}", requestId);
        }
    }

    private async Task PublishRequestReviewNotificationAsync(
        Guid userId,
        RequestType type,
        bool approved,
        string reviewerName,
        string? productName)
    {
        try
        {
            var (typeEn, typeAr) = GetRequestTypeLabels(type);
            var notifType = approved ? NotificationType.RequestApproved : NotificationType.RequestRejected;

            var titleEn = approved ? $"Request approved: {typeEn}" : $"Request rejected: {typeEn}";
            var titleAr = approved ? $"تمت الموافقة على الطلب: {typeAr}" : $"تم رفض الطلب: {typeAr}";

            var subjectSuffixEn = string.IsNullOrWhiteSpace(productName) ? string.Empty : $" — {productName}";
            var subjectSuffixAr = string.IsNullOrWhiteSpace(productName) ? string.Empty : $" — {productName}";

            var bodyEn = approved
                ? $"Approved by {reviewerName}{subjectSuffixEn}"
                : $"Rejected by {reviewerName}{subjectSuffixEn}";
            var bodyAr = approved
                ? $"تمت الموافقة بواسطة {reviewerName}{subjectSuffixAr}"
                : $"تم الرفض بواسطة {reviewerName}{subjectSuffixAr}";

            await _notificationService.SendAsync(
                userId,
                notifType,
                titleEn,
                titleAr,
                bodyEn,
                bodyAr,
                link: "/admin/requests");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish request-review notification to user {UserId}", userId);
        }
    }

    private static (string En, string Ar) GetRequestTypeLabels(RequestType type) => type switch
    {
        RequestType.ChangePrice => ("Change Price", "تغيير السعر"),
        RequestType.SetUnitPrice => ("Set Unit Price", "تحديد سعر الوحدة"),
        RequestType.ActivateProduct => ("Activate Product", "تفعيل المنتج"),
        RequestType.ActivateUnit => ("Activate Unit", "تفعيل الوحدة"),
        RequestType.AddProduct => ("Add Product", "إضافة منتج"),
        RequestType.UpdateProduct => ("Update Product", "تحديث منتج"),
        RequestType.AddUnit => ("Add Unit", "إضافة وحدة"),
        RequestType.UpdateUnit => ("Update Unit", "تحديث وحدة"),
        RequestType.AddGRN => ("Add GRN", "إضافة إذن استلام"),
        RequestType.AddStockAdjustment => ("Add Stock Adjustment", "إضافة تسوية مخزون"),
        RequestType.AddStockTransfer => ("Add Stock Transfer", "إضافة نقل مخزون"),
        RequestType.DeleteProduct => ("Delete Product", "حذف منتج"),
        RequestType.DeleteUnit => ("Delete Unit", "حذف وحدة"),
        RequestType.SetLogisticsDetails => ("Set Logistics Details", "تحديد تفاصيل اللوجستيات"),
        RequestType.AddPurchaseRequest => ("Purchase Request", "طلب شراء"),
        _ => (type.ToString(), type.ToString())
    };

    /// <summary>Resolves the PurchaseRequest linked to an AddPurchaseRequest mirror row from its NewDataJson payload.</summary>
    private async Task<PurchaseRequest?> ResolveLinkedPurchaseRequestAsync(string newDataJson)
    {
        try
        {
            var data = JsonSerializer.Deserialize<JsonElement>(newDataJson);
            if (data.TryGetProperty("PurchaseRequestId", out var prIdEl) && prIdEl.TryGetGuid(out var prId))
                return await _context.PurchaseRequests.FirstOrDefaultAsync(p => p.Id == prId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve linked purchase request from request payload");
        }
        return null;
    }

    /// <summary>
    /// Transfers all media uploaded against the request entity to the target product.
    /// Files on disk remain in place; only DB metadata is updated.
    /// </summary>
    private async Task TransferMediaToProductAsync(Guid requestId, Guid productId)
    {
        var requestMedia = await _context.Media
            .Where(m => m.EntityId == requestId && m.EntityType == EntityType.Request)
            .ToListAsync();

        var requestMainByCollection = requestMedia
            .Where(m => m.IsMain)
            .Select(m => m.CollectionName)
            .Distinct()
            .ToList();

        if (requestMainByCollection.Count > 0)
        {
            var productMedia = await _context.Media
                .Where(m => m.EntityId == productId
                         && m.EntityType == EntityType.Product
                         && requestMainByCollection.Contains(m.CollectionName))
                .ToListAsync();

            foreach (var media in productMedia)
                media.IsMain = false;
        }

        foreach (var media in requestMedia)
        {
            media.EntityType = EntityType.Product;
            media.EntityId = productId;
        }
    }

    private static List<Guid> GetGuidArray(JsonElement data, string propertyName)
    {
        if (!data.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            return new List<Guid>();

        var values = new List<Guid>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var parsed))
                values.Add(parsed);
            else if (item.ValueKind == JsonValueKind.String)
                continue;
            else if (item.ValueKind != JsonValueKind.Null)
                values.Add(item.GetGuid());
        }

        return values;
    }

    private async Task NormalizeProductMainImageAsync(Guid productId, Guid? preferredMainImageId = null)
    {
        var images = await _context.Media
            .Where(m => m.EntityId == productId && m.EntityType == EntityType.Product && m.CollectionName == "image")
            .OrderBy(m => m.Order)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync();

        if (images.Count == 0)
            return;

        Guid mainId;
        if (preferredMainImageId.HasValue && images.Any(i => i.Id == preferredMainImageId.Value))
        {
            mainId = preferredMainImageId.Value;
        }
        else
        {
            mainId = images.FirstOrDefault(i => i.IsMain)?.Id ?? images[0].Id;
        }

        foreach (var image in images)
            image.IsMain = image.Id == mainId;
    }

    public async Task<PaginatedList<RequestDto>> GetMyRequestsAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        RequestType? type = null,
        RequestStatus? status = null)
    {
        var (userId, _) = await _currentUserService.GetCurrentUserAsync();

        var query = _context.Requests
            .Where(r => r.RequestedById == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r =>
                (r.ProductName != null && r.ProductName.ToLower().Contains(s)) ||
                r.RequestedByName.ToLower().Contains(s));
        }

        if (type.HasValue)
            query = query.Where(r => r.Type == type.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        query = query.OrderByDescending(r =>
            r.ApprovedAt.HasValue && r.RejectedAt.HasValue
                ? (r.ApprovedAt.Value > r.RejectedAt.Value ? r.ApprovedAt.Value : r.RejectedAt.Value)
                : r.ApprovedAt ?? r.RejectedAt ?? r.UpdatedAt ?? r.CreatedAt);

        var count = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(MapToDto).ToList();
        return new PaginatedList<RequestDto>(dtos, count, pageNumber, pageSize);
    }

    public async Task<bool> DeleteRequestAsync(Guid id)
    {
        var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null)
            return false;

        if (entity.Status != RequestStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be deleted.");

        _context.Requests.Remove(entity);
        await _context.SaveChangesAsync();
        await LogEntityAsync("Request", entity.Id, AuditAction.Deleted, BuildRequestAuditDetails(entity));
        return true;
    }

    public async Task<bool> DeleteMyRequestAsync(Guid id)
    {
        var (userId, _) = await _currentUserService.GetCurrentUserAsync();

        var entity = await _context.Requests.FirstOrDefaultAsync(r => r.Id == id);
        if (entity == null)
            return false;

        if (entity.RequestedById != userId)
            throw new UnauthorizedAccessException("You can only delete your own requests.");

        if (entity.Status != RequestStatus.Pending)
            throw new InvalidOperationException("Only pending requests can be deleted.");

        _context.Requests.Remove(entity);
        await _context.SaveChangesAsync();
        await LogEntityAsync("Request", entity.Id, AuditAction.Deleted, BuildRequestAuditDetails(entity));
        return true;
    }

    private static RequestDto MapToDto(Request r) => new()
    {
        Id = r.Id,
        Type = r.Type,
        Status = r.Status,
        RequestedById = r.RequestedById,
        RequestedByName = r.RequestedByName,
        ApprovedById = r.ApprovedById,
        ApprovedByName = r.ApprovedByName,
        ApprovedAt = r.ApprovedAt,
        RejectedById = r.RejectedById,
        RejectedByName = r.RejectedByName,
        RejectedAt = r.RejectedAt,
        ReviewNote = r.ReviewNote,
        ProductId = r.ProductId,
        ProductName = r.ProductName,
        CurrentPrice = r.CurrentPrice,
        NewPrice = r.NewPrice,
        UnitId = r.UnitId,
        Note = r.Note,
        NewDataJson = r.NewDataJson,
        OldDataJson = r.OldDataJson,
        CreatedAt = r.CreatedAt
    };

    private async Task LogEntityAsync(string entityName, Guid entityId, AuditAction action)
    {
        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();
        if (userId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId.ToString(),
                Details = null
            });
        }
    }

    private async Task LogRequestAsync(Request request, AuditAction action)
    {
        await LogEntityAsync("Request", request.Id, action, BuildRequestAuditDetails(request));
    }

    private async Task LogEntityAsync(string entityName, Guid entityId, AuditAction action, string? details)
    {
        var (userId, userName) = await _currentUserService.GetCurrentUserAsync();
        if (userId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId.ToString(),
                Details = details
            });
        }
    }

    private static string BuildRequestAuditDetails(Request request)
    {
        var label = string.IsNullOrWhiteSpace(request.ProductName) ? null : request.ProductName;
        return string.IsNullOrWhiteSpace(label)
            ? $"{request.Type} ({request.Status})"
            : $"{request.Type}: {label} ({request.Status})";
    }
}
