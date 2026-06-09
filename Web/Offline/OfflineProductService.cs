using Web.Models.Common;
using Web.Models.Enums;
using Web.Models.Offline;
using Web.Models.Products;
using Web.Services;

namespace Web.Offline;

// Offline-aware decorator around the existing IProductService. Reads from
// IndexedDB filtered by the active store when a local cache exists; falls
// through to the wrapped online service otherwise. Writes/mutations always
// flow through the inner service — products are admin-managed and the cashier
// panel doesn't mutate them.
public class OfflineProductService : IProductService
{
    private readonly IProductService _inner;
    private readonly IIndexedDbService _idb;
    private readonly ActiveStoreContext _activeStore;
    private readonly OfflineNetworkMonitor _network;

    public OfflineProductService(
        IProductService inner,
        IIndexedDbService idb,
        ActiveStoreContext activeStore,
        OfflineNetworkMonitor network)
    {
        _inner = inner;
        _idb = idb;
        _activeStore = activeStore;
        _network = network;
    }

    public async Task<PaginatedList<ProductDto>> GetAllProductsAsync(
        int pageNumber, int pageSize, string? search = null, Guid? categoryId = null,
        bool? isActive = null, Guid? warehouseId = null, bool? onlyWithStock = null,
        ItemStatus? status = null)
    {
        // ActiveStoreContext is the live source of truth for which store the
        // cashier is operating in. The caller's warehouseId is usually loaded
        // from the cashier profile at page-init and doesn't refresh on switch,
        // so we override it here for both online and offline reads — the
        // server uses the warehouseId to scope stock/availability, so passing
        // the fresh value ensures quantities update right after a switch.
        // EnsureLoaded rehydrates the switched store from localStorage on a
        // hard reload so we don't silently fall back to the caller's warehouseId
        // (which is the cashier's first-assigned store, not the switched one).
        await _activeStore.EnsureLoadedAsync();
        var effectiveStoreId = _activeStore.StoreId ?? warehouseId;

        if (await _network.IsOnlineAsync())
        {
            try { return await _inner.GetAllProductsAsync(pageNumber, pageSize, search, categoryId, isActive, effectiveStoreId, onlyWithStock, status); }
            catch { /* server unreachable behind navigator.onLine === true — fall through to cache */ }
        }

        var scopedStoreId = effectiveStoreId;

        try
        {
            var cached = scopedStoreId.HasValue
                ? await _idb.GetByIndexAsync<OfflineProductDto>(
                    OfflineStores.Products, OfflineStores.Indexes.ProductsByStore, scopedStoreId.Value)
                : await _idb.GetAllAsync<OfflineProductDto>(OfflineStores.Products);

            // Mirror the online ProductService ordering exactly so the cashier
            // sees the same product sequence whether the device is online or
            // offline.
            var mapped = cached
                .Select(MapToProductDto)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .ToList();
            var filtered = ApplyFilters(mapped, search, categoryId, isActive, onlyWithStock, status);

            var total = filtered.Count;
            var page = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return new PaginatedList<ProductDto>(page, total, pageNumber, pageSize);
        }
        catch
        {
            return new PaginatedList<ProductDto>(new List<ProductDto>(), 0, pageNumber, pageSize);
        }
    }

    public Task<ProductDetailDto?> GetProductByIdAsync(Guid id)        => _inner.GetProductByIdAsync(id);
    public Task<ProductDto?>       GetProductByCodeAsync(string code)  => _inner.GetProductByCodeAsync(code);
    public Task<ProductDto>        CreateProductAsync(CreateProductRequest request) => _inner.CreateProductAsync(request);
    public Task<ProductDto>        UpdateProductAsync(Guid id, UpdateProductRequest request) => _inner.UpdateProductAsync(id, request);
    public Task                    DeleteProductAsync(Guid id)         => _inner.DeleteProductAsync(id);
    public Task<bool>              CheckCodeExistsAsync(string code, Guid? excludeProductId = null) => _inner.CheckCodeExistsAsync(code, excludeProductId);

    // ---------------- internals ----------------

    private static ProductDto MapToProductDto(OfflineProductDto src) => new()
    {
        Id = src.ProductId,
        NameEn = src.NameEn,
        NameAr = src.NameAr,
        Code = src.Code,
        CategoryId = src.CategoryId,
        CategoryNameEn = src.CategoryNameEn,
        CategoryNameAr = src.CategoryNameAr,
        ImageUrl = src.ThumbnailUrl ?? src.ImageUrl,
        IsActive = true,
        Status = ItemStatus.Active,
        TotalStock = src.AvailableQuantity,
        CreatedAt = src.CreatedAt,
        UpdatedAt = src.UpdatedAt,
        Units = src.Units.Select(u => new ProductUnitDto
        {
            Id = u.UnitId,
            UnitOfMeasureNameEn = u.UnitNameEn,
            UnitOfMeasureNameAr = u.UnitNameAr,
            Barcode = u.Barcode,
            SellingPrice = u.SellingPrice,
            // Index.razor's MaxSellableSellingUnits divides AvailableQuantity by
            // Quantity — a zero here makes every unit look out-of-stock.
            Quantity = u.Quantity,
            LowStockThreshold = u.LowStockThreshold,
            AvailableQuantity = u.AvailableQuantity,
            IsActive = u.IsActive
        }).ToList()
    };

    private static List<ProductDto> ApplyFilters(
        List<ProductDto> source,
        string? search,
        Guid? categoryId,
        bool? isActive,
        bool? onlyWithStock,
        ItemStatus? status)
    {
        IEnumerable<ProductDto> q = source;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p =>
                (p.NameEn?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.NameAr?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Code?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                p.Units.Any(u => !string.IsNullOrEmpty(u.Barcode) && u.Barcode.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId.Value);
        if (isActive.HasValue)   q = q.Where(p => p.IsActive == isActive.Value);
        if (status.HasValue)     q = q.Where(p => p.Status == status.Value);
        if (onlyWithStock == true) q = q.Where(p => p.TotalStock > 0);

        return q.ToList();
    }
}
