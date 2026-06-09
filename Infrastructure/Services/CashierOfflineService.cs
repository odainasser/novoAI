using Application.Common.Interfaces;
using Application.Features.CashierOffline;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Server side of the cashier offline layer: builds the one-shot payload that
// hydrates the cashier panel's IndexedDB cache.
public class CashierOfflineService : ICashierOfflineService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentityService _identityService;
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CashierOfflineService> _logger;

    public CashierOfflineService(
        UserManager<ApplicationUser> userManager,
        IIdentityService identityService,
        ApplicationDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CashierOfflineService> logger)
    {
        _userManager = userManager;
        _identityService = identityService;
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<CashierOfflineDataResponse?> GetOfflineDataAsync(Guid userId, int orderHistoryDays = 30, int credentialLifetimeDays = 7)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;

        var roles = await _identityService.GetUserRolesAsync(user.Id);
        if (!roles.Contains(Roles.Cashier)) return null;

        var permissions = await _identityService.GetUserPermissionsAsync(user.Id);

        var assignedStoreIds = await _db.CashierWarehouses
            .AsNoTracking()
            .Where(cw => cw.CashierId == user.Id)
            .Select(cw => cw.WarehouseId)
            .ToListAsync();

        // Backwards-compat: cashiers seeded before the CashierWarehouses junction
        // existed have only ApplicationUser.WarehouseId set. Without this fallback
        // they'd get an empty store/product cache and offline mode would be useless.
        if (assignedStoreIds.Count == 0 && user.WarehouseId.HasValue)
        {
            assignedStoreIds = new List<Guid> { user.WarehouseId.Value };
        }

        var stores = await _db.Warehouses
            .AsNoTracking()
            .Where(w => assignedStoreIds.Contains(w.Id))
            .Select(w => new OfflineStoreDto
            {
                StoreId = w.Id,
                NameEn = w.NameEn,
                NameAr = w.NameAr,
                BranchNameEn = w.Branch != null ? w.Branch.NameEn : null,
                BranchNameAr = w.Branch != null ? w.Branch.NameAr : null,
                Type = w.WarehouseType != null ? w.WarehouseType.Code : null
            })
            .ToListAsync();

        // Cache shape mirrors the online cashier view: every assigned store
        // gets a row per active product that has at least one selling unit,
        // even when current stock is zero — admins can restock at any time
        // and the cashier needs the catalog regardless.
        //
        // Match the online ProductService ordering — UpdatedAt ?? CreatedAt
        // DESC — so cached and live lists render in the exact same sequence.
        var dbProducts = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Status == ItemStatus.Active && p.IsActive)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync();

        var productIds = dbProducts.Select(p => p.Id).ToList();

        // Match the online ProductService filter exactly: any non-rejected
        // selling unit qualifies, so cached and live product lists align.
        // The POS itself filters by per-unit IsActive when rendering, so we
        // don't need to pre-filter here.
        var sellingUnits = await _db.Units
            .AsNoTracking()
            .Include(u => u.UnitOfMeasure)
            .Where(u => productIds.Contains(u.ProductId)
                        && u.Status != ItemStatus.Rejected
                        && u.UnitUnitTypes.Any(uut => uut.UnitType != null && uut.UnitType.Code == "UT_SELLING"))
            .ToListAsync();

        var unitsByProduct = sellingUnits.GroupBy(u => u.ProductId).ToDictionary(g => g.Key, g => g.ToList());
        var sellingUnitIds = sellingUnits.Select(u => u.Id).ToHashSet();

        var stockRows = await _db.StockBalances
            .AsNoTracking()
            .Where(sb => assignedStoreIds.Contains(sb.WarehouseId) && sellingUnitIds.Contains(sb.UnitId))
            .Select(sb => new { sb.WarehouseId, sb.UnitId, sb.AvailableQuantity })
            .ToListAsync();

        var stockByStoreUnit = stockRows
            .GroupBy(s => new { s.WarehouseId, s.UnitId })
            .ToDictionary(g => (g.Key.WarehouseId, g.Key.UnitId), g => g.Sum(x => x.AvailableQuantity));

        var mediaByProduct = await BuildProductMediaMapAsync(productIds);
        var baseUrl = BuildApiBaseUrl();

        var products = new List<OfflineProductDto>(dbProducts.Count * assignedStoreIds.Count);
        foreach (var storeId in assignedStoreIds)
        {
            foreach (var product in dbProducts)
            {
                if (!unitsByProduct.TryGetValue(product.Id, out var productUnits) || productUnits.Count == 0)
                    continue;

                mediaByProduct.TryGetValue(product.Id, out var mediaInfo);

                var unitDtos = productUnits.Select(u => new OfflineProductUnitDto
                {
                    UnitId = u.Id,
                    UnitNameEn = u.UnitOfMeasure?.NameEn,
                    UnitNameAr = u.UnitOfMeasure?.NameAr,
                    Barcode = u.Barcode,
                    SellingPrice = u.SellingPrice,
                    Quantity = u.Quantity,
                    LowStockThreshold = u.LowStockThreshold,
                    AvailableQuantity = stockByStoreUnit.GetValueOrDefault((storeId, u.Id), 0),
                    IsActive = u.IsActive && u.Status == ItemStatus.Active
                }).ToList();

                products.Add(new OfflineProductDto
                {
                    ProductId = product.Id,
                    StoreId = storeId,
                    Code = product.Code,
                    NameEn = product.NameEn,
                    NameAr = product.NameAr,
                    CategoryId = product.CategoryId,
                    CategoryNameEn = product.Category?.NameEn,
                    CategoryNameAr = product.Category?.NameAr,
                    ImageUrl = mediaInfo?.Url,
                    ThumbnailUrl = mediaInfo is null ? null : $"{baseUrl}/api/products/{product.Id}/thumbnail",
                    ImageETag = mediaInfo?.ETag,
                    AvailableQuantity = unitDtos.Sum(x => x.AvailableQuantity),
                    CreatedAt = product.CreatedAt,
                    UpdatedAt = product.UpdatedAt,
                    Units = unitDtos
                });
            }
        }

        var shifts = await _db.Shifts
            .AsNoTracking()
            .Where(s => s.CashierId == user.Id)
            .OrderByDescending(s => s.StartTime)
            .Take(50)
            .Select(s => new OfflineShiftDto
            {
                ShiftId = s.Id,
                CashierId = s.CashierId,
                StoreId = s.WarehouseId,
                StoreNameEn = s.WarehouseNameEn,
                StoreNameAr = s.WarehouseNameAr,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                CashIn = s.CashIn,
                CashOut = s.CashOut,
                TotalSales = s.TotalSales,
                TotalReturns = s.TotalReturns,
                Status = s.Status.ToString(),
                Comments = s.Comments
            })
            .ToListAsync();

        var sinceUtc = DateTime.UtcNow.AddDays(-Math.Max(1, orderHistoryDays));
        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Refunds)
            .Where(o => o.CashierId == user.Id && o.CreatedAt >= sinceUtc)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OfflineOrderDto
            {
                OrderId = o.Id,
                StoreId = o.WarehouseId,
                StoreNameEn = o.WarehouseNameEn,
                StoreNameAr = o.WarehouseNameAr,
                OrderNumber = o.OrderNumber,
                Status = o.Status.ToString(),
                PaymentMethod = o.PaymentMethod.ToString(),
                CashAmount = o.CashAmount,
                CardAmount = o.CardAmount,
                Subtotal = o.Subtotal,
                VatRate = o.VatRate,
                VatAmount = o.VatAmount,
                Total = o.Total,
                CreatedAt = o.CreatedAt,
                CashierId = o.CashierId,
                CashierName = o.CashierName,
                Items = o.Items.Select(i => new OfflineOrderItemDto
                {
                    OrderItemId = i.Id,
                    ProductId = i.ProductId,
                    ProductNameEn = i.ProductNameEn,
                    ProductNameAr = i.ProductNameAr,
                    ProductCode = i.ProductCode,
                    UnitId = i.UnitId,
                    UnitNameEn = i.UnitNameEn,
                    UnitBarcode = i.UnitBarcode,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Total = i.Total
                }).ToList(),
                Refunds = o.Refunds.Select(r => new OfflineOrderRefundDto
                {
                    Id = r.Id,
                    Amount = r.Amount,
                    CreatedAt = r.CreatedAt,
                    Reason = r.Reason
                }).ToList()
            })
            .ToListAsync();

        var now = DateTime.UtcNow;
        var displayName = GetDisplayName(user);

        return new CashierOfflineDataResponse
        {
            ServerUtcNow = now,
            Credential = new OfflineCredentialDto
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList(),
                Permissions = permissions.ToList(),
                AssignedStoreIds = assignedStoreIds,
                IssuedAtUtc = now,
                ExpiresAtUtc = now.AddDays(Math.Max(1, credentialLifetimeDays))
            },
            Profile = new OfflineProfileDto
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = displayName,
                CanRefund = user.CanRefund
            },
            Stores = stores,
            Products = products,
            Shifts = shifts,
            Orders = orders
        };
    }

    private static string GetDisplayName(ApplicationUser user)
    {
        var first = user.FirstName ?? string.Empty;
        var last = user.LastName ?? string.Empty;
        var full = $"{first} {last}".Trim();
        if (!string.IsNullOrEmpty(full)) return full;
        return user.Email ?? user.UserName ?? user.Id.ToString();
    }

    private async Task<Dictionary<Guid, MediaInfo>> BuildProductMediaMapAsync(List<Guid> productIds)
    {
        if (productIds.Count == 0) return new();

        var rows = await _db.Media
            .AsNoTracking()
            .Where(m => m.EntityType == EntityType.Product && productIds.Contains(m.EntityId))
            .OrderByDescending(m => m.IsMain)
            .ThenBy(m => m.Order)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync();

        var baseUrl = BuildApiBaseUrl();
        var map = new Dictionary<Guid, MediaInfo>();
        foreach (var media in rows)
        {
            if (map.ContainsKey(media.EntityId)) continue;
            var path = media.Path.TrimStart('/');
            var url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : $"{baseUrl}/{path}";
            // ETag derived from a stable per-media value so we don't need to hash file bytes on every request.
            var etag = $"\"{media.Id:N}-{media.Size}-{(media.UpdatedAt ?? media.CreatedAt).Ticks}\"";
            map[media.EntityId] = new MediaInfo(url, etag);
        }

        return map;
    }

    private string BuildApiBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null) return string.Empty;
        return $"{request.Scheme}://{request.Host}";
    }

    private sealed record MediaInfo(string Url, string ETag);
}
