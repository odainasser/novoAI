using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class CatalogSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CatalogSeeder> _logger;

    public CatalogSeeder(ApplicationDbContext context, ILogger<CatalogSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting catalog seeding...");

        await SeedCategoriesAsync();
        await SeedProductsAsync();
        await SeedUnitsAsync();
        await SeedStockBalancesAsync();
        await FixActiveStatusAsync();

        _logger.LogInformation("Catalog seeding completed.");
    }

    private async Task SeedCategoriesAsync()
    {
        // Category IDs are kept stable so existing FK references to category codes 1–6 still resolve.
        var categories = new List<Category>
        {
            new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"), NameEn = "Beverages",                   NameAr = "المشروبات",                  SortOrder = 1, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"), NameEn = "Snacks & Confectionery",      NameAr = "الوجبات الخفيفة والحلويات",  SortOrder = 2, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"), NameEn = "Dairy & Eggs",                NameAr = "الألبان والبيض",             SortOrder = 3, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"), NameEn = "Bakery",                      NameAr = "المخبوزات",                  SortOrder = 4, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"), NameEn = "Pantry & Grocery",            NameAr = "البقالة والمؤن",             SortOrder = 5, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000006"), NameEn = "Household & Personal Care",   NameAr = "المنزل والعناية الشخصية",    SortOrder = 6, IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
        };

        var existingNames = await _context.Categories
            .IgnoreQueryFilters()
            .Select(c => c.NameEn)
            .ToListAsync();

        var newCategories = categories.Where(c => !existingNames.Contains(c.NameEn)).ToList();

        if (newCategories.Count == 0)
        {
            _logger.LogInformation("All categories already exist. Skipping.");
            return;
        }

        await _context.Categories.AddRangeAsync(newCategories);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} categories.", newCategories.Count);
    }

    private async Task SeedProductsAsync()
    {
        var catBeverages    = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var catSnacks       = Guid.Parse("a1000000-0000-0000-0000-000000000002");
        var catDairy        = Guid.Parse("a1000000-0000-0000-0000-000000000003");
        var catBakery       = Guid.Parse("a1000000-0000-0000-0000-000000000004");
        var catPantry       = Guid.Parse("a1000000-0000-0000-0000-000000000005");
        var catHousehold    = Guid.Parse("a1000000-0000-0000-0000-000000000006");

        var products = new List<Product>
        {
            // Beverages
            new() { NameEn = "Mineral Water 1.5L",          NameAr = "ماء معدني 1.5 لتر",          Code = "BEV-001", CategoryId = catBeverages, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Orange Juice 1L",             NameAr = "عصير برتقال 1 لتر",          Code = "BEV-002", CategoryId = catBeverages, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Cola Can 330ml",              NameAr = "كولا علبة 330 مل",            Code = "BEV-003", CategoryId = catBeverages, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Arabic Coffee 250g",          NameAr = "قهوة عربية 250 جم",           Code = "BEV-004", CategoryId = catBeverages, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Tea Bags 100ct",              NameAr = "أكياس شاي 100 قطعة",          Code = "BEV-005", CategoryId = catBeverages, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },

            // Snacks & Confectionery
            new() { NameEn = "Potato Chips 150g",           NameAr = "رقائق بطاطس 150 جم",          Code = "SNK-001", CategoryId = catSnacks,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Chocolate Bar 100g",          NameAr = "لوح شوكولاتة 100 جم",         Code = "SNK-002", CategoryId = catSnacks,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Salted Mixed Nuts 250g",      NameAr = "مكسرات مملحة مشكلة 250 جم",   Code = "SNK-003", CategoryId = catSnacks,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Biscuits Family Pack",        NameAr = "بسكويت عبوة عائلية",          Code = "SNK-004", CategoryId = catSnacks,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },

            // Dairy & Eggs
            new() { NameEn = "Fresh Milk 1L",               NameAr = "حليب طازج 1 لتر",             Code = "DRY-001", CategoryId = catDairy,     IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Greek Yogurt 500g",           NameAr = "زبادي يوناني 500 جم",         Code = "DRY-002", CategoryId = catDairy,     IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Cheddar Cheese 200g",         NameAr = "جبنة شيدر 200 جم",            Code = "DRY-003", CategoryId = catDairy,     IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Eggs Tray (30 pcs)",          NameAr = "علبة بيض (30 حبة)",            Code = "DRY-004", CategoryId = catDairy,     IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },

            // Bakery
            new() { NameEn = "Arabic Bread Pack (6 pcs)",   NameAr = "خبز عربي عبوة (6 قطع)",        Code = "BAK-001", CategoryId = catBakery,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Croissant 4-Pack",            NameAr = "كرواسون عبوة 4 قطع",           Code = "BAK-002", CategoryId = catBakery,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Whole Wheat Bread 700g",      NameAr = "خبز قمح كامل 700 جم",          Code = "BAK-003", CategoryId = catBakery,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },

            // Pantry & Grocery
            new() { NameEn = "Basmati Rice 5kg",            NameAr = "أرز بسمتي 5 كجم",              Code = "PAN-001", CategoryId = catPantry,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Sugar 2kg",                   NameAr = "سكر 2 كجم",                    Code = "PAN-002", CategoryId = catPantry,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Olive Oil 1L",                NameAr = "زيت زيتون 1 لتر",              Code = "PAN-003", CategoryId = catPantry,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Pasta Spaghetti 500g",        NameAr = "معكرونة سباغيتي 500 جم",       Code = "PAN-004", CategoryId = catPantry,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Canned Tuna 185g",            NameAr = "تونة معلبة 185 جم",             Code = "PAN-005", CategoryId = catPantry,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Iodized Salt 1kg",            NameAr = "ملح مُيوَّد 1 كجم",            Code = "PAN-006", CategoryId = catPantry,    IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },

            // Household & Personal Care
            new() { NameEn = "Laundry Detergent 3kg",       NameAr = "مسحوق غسيل 3 كجم",             Code = "HHP-001", CategoryId = catHousehold, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Dish Soap 750ml",             NameAr = "سائل غسيل صحون 750 مل",        Code = "HHP-002", CategoryId = catHousehold, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Toilet Paper 12-Pack",        NameAr = "ورق تواليت 12 لفة",             Code = "HHP-003", CategoryId = catHousehold, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Toothpaste 100ml",            NameAr = "معجون أسنان 100 مل",            Code = "HHP-004", CategoryId = catHousehold, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
            new() { NameEn = "Shampoo 400ml",               NameAr = "شامبو 400 مل",                  Code = "HHP-005", CategoryId = catHousehold, IsActive = true, Status = ItemStatus.Active, CreatedAt = DateTime.UtcNow, CreatedBy = "System" },
        };

        var existingCodes = await _context.Products
            .IgnoreQueryFilters()
            .Select(p => p.Code)
            .ToListAsync();

        var newProducts = products.Where(p => !existingCodes.Contains(p.Code)).ToList();

        if (newProducts.Count == 0)
        {
            _logger.LogInformation("All products already exist. Skipping.");
            return;
        }

        await _context.Products.AddRangeAsync(newProducts);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} products.", newProducts.Count);
    }

    private async Task SeedUnitsAsync()
    {
        // Resolve UOM lookup IDs
        var uomRoot = await _context.Lookups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "UNIT_OF_MEASURE" && l.ParentId == null);
        if (uomRoot == null) { _logger.LogWarning("UOM root lookup not found. Skipping units."); return; }

        var uomLookups = await _context.Lookups.IgnoreQueryFilters()
            .Where(l => l.ParentId == uomRoot.Id)
            .ToListAsync();

        var uomPiece = uomLookups.FirstOrDefault(l => l.Code == "UOM_PIECE")?.Id;
        var uomBox   = uomLookups.FirstOrDefault(l => l.Code == "UOM_BOX")?.Id;
        var uomPack  = uomLookups.FirstOrDefault(l => l.Code == "UOM_PACK")?.Id;
        var uomSet   = uomLookups.FirstOrDefault(l => l.Code == "UOM_SET")?.Id;
        var uomPair  = uomLookups.FirstOrDefault(l => l.Code == "UOM_PAIR")?.Id;

        if (uomPiece == null) { _logger.LogWarning("UOM_PIECE lookup not found. Skipping units."); return; }

        // Resolve Unit Type lookup IDs
        var unitTypeRoot = await _context.Lookups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "UNIT_TYPE" && l.ParentId == null);
        if (unitTypeRoot == null) { _logger.LogWarning("UNIT_TYPE root lookup not found. Skipping units."); return; }

        var unitTypeLookups = await _context.Lookups.IgnoreQueryFilters()
            .Where(l => l.ParentId == unitTypeRoot.Id)
            .ToListAsync();

        var utSelling   = unitTypeLookups.FirstOrDefault(l => l.Code == "UT_SELLING")?.Id;
        var utLogistics = unitTypeLookups.FirstOrDefault(l => l.Code == "UT_LOGISTICS")?.Id;

        if (utSelling == null) { _logger.LogWarning("UT_SELLING lookup not found. Skipping units."); return; }
        if (utLogistics == null) { _logger.LogWarning("UT_LOGISTICS lookup not found. Skipping units."); return; }

        // Get all seeded products by Code
        var products = await _context.Products.IgnoreQueryFilters()
            .Where(p => p.IsActive)
            .ToListAsync();

        if (products.Count == 0) { _logger.LogInformation("No products found. Skipping unit seeding."); return; }

        var productByCode = products.ToDictionary(p => p.Code, p => p.Id);

        // Check existing units to avoid duplicates
        var existingBarcodes = await _context.Units.IgnoreQueryFilters()
            .Select(su => su.Barcode)
            .ToListAsync();
        var existingSet = new HashSet<string>(existingBarcodes);

        var units = new List<Unit>();
        var unitUnitTypes = new List<UnitUnitType>();
        var unitSuppliers = new List<UnitSupplier>();
        var now = DateTime.UtcNow;

        // Load suppliers for seeding
        var suppliers = await _context.Suppliers.IgnoreQueryFilters()
            .Where(s => s.IsActive)
            .ToListAsync();
        var supplierByName = suppliers.ToDictionary(s => s.NameEn, s => s.Id);

        void Add(string code, Guid uomId, int qty, decimal sellingPrice, decimal cost, string barcodeSuffix,
            string sellingBarcodeSuffix, string[] supplierNames, int lowStockThreshold, params Guid[] typeIds)
        {
            if (!productByCode.TryGetValue(code, out var productId)) return;
            var barcode = $"{code}-{barcodeSuffix}";
            if (existingSet.Contains(barcode)) return;
            var unitId = Guid.NewGuid();
            units.Add(new Unit
            {
                Id = unitId,
                ProductId = productId,
                UnitOfMeasureId = uomId,
                Quantity = qty,
                LowStockThreshold = lowStockThreshold,
                SellingPrice = sellingPrice,
                Cost = cost,
                Barcode = barcode,
                SellingBarcode = string.IsNullOrEmpty(sellingBarcodeSuffix) ? string.Empty : $"{code}-S-{sellingBarcodeSuffix}",
                IsActive = true,
                Status = ItemStatus.Active,
                CreatedAt = now,
                CreatedBy = "System"
            });
            foreach (var typeId in typeIds)
            {
                unitUnitTypes.Add(new UnitUnitType { UnitId = unitId, UnitTypeId = typeId });
            }
            // Add unit suppliers
            foreach (var supplierName in supplierNames)
            {
                if (supplierByName.TryGetValue(supplierName, out var supplierId))
                {
                    unitSuppliers.Add(new UnitSupplier
                    {
                        UnitId = unitId,
                        SupplierId = supplierId,
                        Barcode = $"{code}-SUP-{barcodeSuffix}"
                    });
                }
            }
        }

        // ── Beverages ─────────────────────────────────────
        // Mineral Water 1.5L — sold as piece; case-of-6 logistics from Gulf Dairy & Beverages
        Add("BEV-001", uomPiece.Value, 1,   2.50m, 1.20m,  "PC",  "PC", [],                          25, utSelling.Value);
        if (uomBox != null)  Add("BEV-001", uomBox.Value,  6,  0m,      6.50m,  "BX6", "", ["Gulf Dairy & Beverages"],  5, utLogistics.Value);

        // Orange Juice 1L — three selling units to demo mixed stock states
        Add("BEV-002", uomPiece.Value, 1,   8.00m, 4.50m,  "PC",  "PC", [],                          15, utSelling.Value);
        if (uomPack != null) Add("BEV-002", uomPack.Value, 4,  30.00m, 17.00m, "PK4", "PK4", ["Gulf Dairy & Beverages"], 4, utSelling.Value, utLogistics.Value);
        if (uomBox != null)  Add("BEV-002", uomBox.Value,  12, 0m,     50.00m, "BX12", "", ["Gulf Dairy & Beverages"],  3, utLogistics.Value);

        // Cola Can 330ml
        Add("BEV-003", uomPiece.Value, 1,   2.00m, 0.90m,  "PC",  "PC", [],                          30, utSelling.Value);
        if (uomPack != null) Add("BEV-003", uomPack.Value, 6,  11.00m, 5.20m,  "PK6", "PK6", ["Gulf Dairy & Beverages"], 6, utSelling.Value, utLogistics.Value);
        if (uomBox != null)  Add("BEV-003", uomBox.Value,  24, 0m,     19.00m, "BX24", "", ["Gulf Dairy & Beverages"],  3, utLogistics.Value);

        // Arabic Coffee 250g
        Add("BEV-004", uomPiece.Value, 1,  18.00m, 9.00m,  "PC",  "PC", [],                          12, utSelling.Value);
        if (uomBox != null)  Add("BEV-004", uomBox.Value,  12, 0m,    100.00m, "BX12", "", ["Fresh Foods Trading Co."], 2, utLogistics.Value);

        // Tea Bags 100ct
        Add("BEV-005", uomPiece.Value, 1,  14.00m, 7.00m,  "PC",  "PC", [],                          15, utSelling.Value);
        if (uomBox != null)  Add("BEV-005", uomBox.Value,  12, 0m,     78.00m, "BX12", "", ["Fresh Foods Trading Co."], 3, utLogistics.Value);

        // ── Snacks & Confectionery ────────────────────────
        // Potato Chips 150g
        Add("SNK-001", uomPiece.Value, 1,   4.00m, 1.80m,  "PC",  "PC", [],                          20, utSelling.Value);
        if (uomBox != null)  Add("SNK-001", uomBox.Value,  24, 0m,     40.00m, "BX24", "", ["Snack World Distribution"], 4, utLogistics.Value);

        // Chocolate Bar 100g
        Add("SNK-002", uomPiece.Value, 1,   6.00m, 2.80m,  "PC",  "PC", [],                          20, utSelling.Value);
        if (uomBox != null)  Add("SNK-002", uomBox.Value,  20, 0m,     52.00m, "BX20", "", ["Snack World Distribution"], 3, utLogistics.Value);

        // Salted Mixed Nuts 250g
        Add("SNK-003", uomPiece.Value, 1,  22.00m, 11.00m, "PC",  "PC", [],                          10, utSelling.Value);
        if (uomBox != null)  Add("SNK-003", uomBox.Value,  12, 0m,    120.00m, "BX12", "", ["Snack World Distribution"], 2, utLogistics.Value);

        // Biscuits Family Pack
        Add("SNK-004", uomPiece.Value, 1,   9.00m, 4.50m,  "PC",  "PC", [],                          15, utSelling.Value);
        if (uomPack != null) Add("SNK-004", uomPack.Value, 6,  50.00m, 24.00m, "PK6", "PK6", ["Snack World Distribution"], 5, utSelling.Value, utLogistics.Value);

        // ── Dairy & Eggs ──────────────────────────────────
        // Fresh Milk 1L
        Add("DRY-001", uomPiece.Value, 1,   6.50m, 3.20m,  "PC",  "PC", [],                          20, utSelling.Value);
        if (uomBox != null)  Add("DRY-001", uomBox.Value,  12, 0m,     35.00m, "BX12", "", ["Gulf Dairy & Beverages"], 4, utLogistics.Value);

        // Greek Yogurt 500g
        Add("DRY-002", uomPiece.Value, 1,   9.00m, 4.20m,  "PC",  "PC", [],                          15, utSelling.Value);
        if (uomPack != null) Add("DRY-002", uomPack.Value, 4,  32.00m, 15.00m, "PK4", "PK4", ["Gulf Dairy & Beverages"], 4, utSelling.Value, utLogistics.Value);

        // Cheddar Cheese 200g
        Add("DRY-003", uomPiece.Value, 1,  15.00m, 7.50m,  "PC",  "PC", [],                          10, utSelling.Value);
        if (uomBox != null)  Add("DRY-003", uomBox.Value,  12, 0m,     82.00m, "BX12", "", ["Gulf Dairy & Beverages"], 3, utLogistics.Value);

        // Eggs Tray (30 pcs)
        Add("DRY-004", uomPiece.Value, 1,  20.00m, 11.00m, "PC",  "PC", [],                          12, utSelling.Value);
        if (uomBox != null)  Add("DRY-004", uomBox.Value,  6,  0m,     60.00m, "BX6",  "", ["Farm Fresh Produce"], 3, utLogistics.Value);

        // ── Bakery ────────────────────────────────────────
        // Arabic Bread Pack (6 pcs)
        Add("BAK-001", uomPiece.Value, 1,   3.50m, 1.50m,  "PC",  "PC", [],                          25, utSelling.Value);
        if (uomBox != null)  Add("BAK-001", uomBox.Value,  20, 0m,     28.00m, "BX20", "", ["National Bakery Supplies"], 5, utLogistics.Value);

        // Croissant 4-Pack
        Add("BAK-002", uomPiece.Value, 1,  12.00m, 6.00m,  "PC",  "PC", [],                          10, utSelling.Value);
        if (uomBox != null)  Add("BAK-002", uomBox.Value,  10, 0m,     55.00m, "BX10", "", ["National Bakery Supplies"], 3, utLogistics.Value);

        // Whole Wheat Bread 700g
        Add("BAK-003", uomPiece.Value, 1,   7.00m, 3.30m,  "PC",  "PC", [],                          15, utSelling.Value);
        if (uomBox != null)  Add("BAK-003", uomBox.Value,  10, 0m,     30.00m, "BX10", "", ["National Bakery Supplies"], 3, utLogistics.Value);

        // ── Pantry & Grocery ──────────────────────────────
        // Basmati Rice 5kg
        Add("PAN-001", uomPiece.Value, 1,  55.00m, 28.00m, "PC",  "PC", [],                          8,  utSelling.Value);
        if (uomBox != null)  Add("PAN-001", uomBox.Value,  4,  0m,    105.00m, "BX4",  "", ["Mediterranean Imports LLC"], 2, utLogistics.Value);

        // Sugar 2kg
        Add("PAN-002", uomPiece.Value, 1,  10.00m, 5.00m,  "PC",  "PC", [],                          20, utSelling.Value);
        if (uomBox != null)  Add("PAN-002", uomBox.Value,  10, 0m,     45.00m, "BX10", "", ["Mediterranean Imports LLC"], 3, utLogistics.Value);

        // Olive Oil 1L
        Add("PAN-003", uomPiece.Value, 1,  38.00m, 18.00m, "PC",  "PC", [],                          12, utSelling.Value);
        if (uomBox != null)  Add("PAN-003", uomBox.Value,  6,  0m,    100.00m, "BX6",  "", ["Mediterranean Imports LLC"], 2, utLogistics.Value);

        // Pasta Spaghetti 500g
        Add("PAN-004", uomPiece.Value, 1,   5.00m, 2.20m,  "PC",  "PC", [],                          18, utSelling.Value);
        if (uomBox != null)  Add("PAN-004", uomBox.Value,  20, 0m,     40.00m, "BX20", "", ["Mediterranean Imports LLC"], 4, utLogistics.Value);

        // Canned Tuna 185g
        Add("PAN-005", uomPiece.Value, 1,   6.50m, 3.00m,  "PC",  "PC", [],                          15, utSelling.Value);
        if (uomPack != null) Add("PAN-005", uomPack.Value, 4,  24.00m, 11.00m, "PK4", "PK4", ["Fresh Foods Trading Co."], 4, utSelling.Value, utLogistics.Value);

        // Iodized Salt 1kg
        Add("PAN-006", uomPiece.Value, 1,   2.50m, 1.00m,  "PC",  "PC", [],                          20, utSelling.Value);
        if (uomBox != null)  Add("PAN-006", uomBox.Value,  20, 0m,     18.00m, "BX20", "", ["Mediterranean Imports LLC"], 4, utLogistics.Value);

        // ── Household & Personal Care ─────────────────────
        // Laundry Detergent 3kg
        Add("HHP-001", uomPiece.Value, 1,  32.00m, 16.00m, "PC",  "PC", [],                          8,  utSelling.Value);
        if (uomBox != null)  Add("HHP-001", uomBox.Value,  4,  0m,     58.00m, "BX4",  "", ["Home Essentials Trading"], 2, utLogistics.Value);

        // Dish Soap 750ml
        Add("HHP-002", uomPiece.Value, 1,   9.50m, 4.50m,  "PC",  "PC", [],                          12, utSelling.Value);
        if (uomBox != null)  Add("HHP-002", uomBox.Value,  12, 0m,     52.00m, "BX12", "", ["Home Essentials Trading"], 3, utLogistics.Value);

        // Toilet Paper 12-Pack
        Add("HHP-003", uomPiece.Value, 1,  22.00m, 11.00m, "PC",  "PC", [],                          10, utSelling.Value);
        if (uomBox != null)  Add("HHP-003", uomBox.Value,  4,  0m,     40.00m, "BX4",  "", ["Home Essentials Trading"], 2, utLogistics.Value);

        // Toothpaste 100ml
        Add("HHP-004", uomPiece.Value, 1,   8.00m, 3.50m,  "PC",  "PC", [],                          15, utSelling.Value);
        if (uomBox != null)  Add("HHP-004", uomBox.Value,  12, 0m,     38.00m, "BX12", "", ["Personal Care Distributors"], 3, utLogistics.Value);

        // Shampoo 400ml
        Add("HHP-005", uomPiece.Value, 1,  18.00m, 8.50m,  "PC",  "PC", [],                          10, utSelling.Value);
        if (uomPack != null) Add("HHP-005", uomPack.Value, 2,  32.00m, 16.00m, "PK2", "PK2", ["Personal Care Distributors"], 3, utSelling.Value, utLogistics.Value);

        if (units.Count == 0)
        {
            _logger.LogInformation("All units already exist. Skipping.");
            return;
        }

        await _context.Units.AddRangeAsync(units);
        await _context.SaveChangesAsync();

        // Seed unit type associations
        await _context.Set<UnitUnitType>().AddRangeAsync(unitUnitTypes);
        await _context.SaveChangesAsync();

        // Seed unit supplier associations
        if (unitSuppliers.Count > 0)
        {
            await _context.UnitSuppliers.AddRangeAsync(unitSuppliers);
            await _context.SaveChangesAsync();
        }
        _logger.LogInformation("Seeded {Count} units with {TypeCount} type associations and {SupplierCount} supplier associations.", units.Count, unitUnitTypes.Count, unitSuppliers.Count);
    }

    private async Task SeedStockBalancesAsync()
    {
        // Get all branch store warehouses
        var warehouseTypeRoot = await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "WAREHOUSE_TYPE" && l.ParentId == null);

        if (warehouseTypeRoot == null) return;

        var branchStoreType = await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "MS" && l.ParentId == warehouseTypeRoot.Id);

        if (branchStoreType == null) return;

        var branchStoreWarehouses = await _context.Warehouses
            .IgnoreQueryFilters()
            .Where(w => w.WarehouseTypeId == branchStoreType.Id && w.BranchId != null && w.IsActive)
            .ToListAsync();

        if (branchStoreWarehouses.Count == 0)
        {
            _logger.LogInformation("No branch store warehouses found. Skipping stock balance seeding.");
            return;
        }

        // Get all seeded units (logistics units)
        var units = await _context.Units
            .IgnoreQueryFilters()
            .Where(u => u.IsActive)
            .ToListAsync();

        if (units.Count == 0)
        {
            _logger.LogInformation("No units found. Skipping stock balance seeding.");
            return;
        }

        // Check existing stock balances
        var existingBalances = await _context.StockBalances
            .IgnoreQueryFilters()
            .Select(sb => new { sb.WarehouseId, sb.UnitId })
            .ToListAsync();

        var existingSet = new HashSet<(Guid, Guid)>(
            existingBalances.Select(b => (b.WarehouseId, b.UnitId)));

        var random = new Random(42); // fixed seed for reproducibility
        var newBalances = new List<StockBalance>();

        // Deterministic test data: force specific SELLING unit barcodes into OOS / Low Stock
        // so the cashier panel and AI widget always have predictable demo cases.
        var forcedOosBarcodes = new HashSet<string>
        {
            "PAN-003-PC",  // Olive Oil 1L — out
            "BEV-004-PC",  // Arabic Coffee 250g — out
            "DRY-004-PC",  // Eggs Tray (30 pcs) — out
            "BEV-002-PC",  // Orange Juice 1L (piece variant) — out  [mixed-state demo]
            "BAK-001-PC",  // Arabic Bread Pack — out
            "HHP-003-PC",  // Toilet Paper 12-Pack — out
        };
        var forcedLowBarcodes = new HashSet<string>
        {
            "BEV-002-PK4", // Orange Juice 1L (pack-of-4 variant) — low  [mixed-state demo]
            "DRY-001-PC",  // Fresh Milk 1L — low
            "SNK-001-PC",  // Potato Chips 150g — low
            "PAN-001-PC",  // Basmati Rice 5kg — low
            "PAN-002-PC",  // Sugar 2kg — low
            "BEV-005-PC",  // Tea Bags 100ct — low
            "HHP-001-PC",  // Laundry Detergent 3kg — low
        };
        // BEV-002-BX12 (case-of-12 variant) is intentionally left healthy so BEV-002 shows
        // OOS + Low + In stock across its three selling units in the unit-pick modal.

        foreach (var warehouse in branchStoreWarehouses)
        {
            foreach (var unit in units)
            {
                if (existingSet.Contains((warehouse.Id, unit.Id)))
                    continue;

                int qty;
                if (forcedOosBarcodes.Contains(unit.Barcode))
                {
                    qty = 0;
                }
                else if (forcedLowBarcodes.Contains(unit.Barcode))
                {
                    // 1 base unit — guarantees AvailableQuantity ≤ LowStockThreshold for any threshold ≥ 1
                    qty = 1;
                }
                else
                {
                    // Healthy stock: comfortably above the threshold for the rest
                    var floor = Math.Max(unit.LowStockThreshold + 1, unit.Quantity + 1);
                    qty = random.Next(floor, floor + 60);
                }

                newBalances.Add(new StockBalance
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = warehouse.Id,
                    UnitId = unit.Id,
                    AvailableQuantity = qty,
                    ReservedQuantity = 0,
                    InTransitQuantity = 0,
                    LastStockCheckDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                });
            }
        }

        if (newBalances.Count == 0)
        {
            _logger.LogInformation("All stock balances already exist. Skipping.");
            return;
        }

        await _context.StockBalances.AddRangeAsync(newBalances);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} stock balances across {WarehouseCount} store branches.",
            newBalances.Count, branchStoreWarehouses.Count);
    }

    private async Task FixActiveStatusAsync()
    {
        var fixedProducts = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.IsActive && p.Status == ItemStatus.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ItemStatus.Active));

        var fixedUnits = await _context.Units
            .IgnoreQueryFilters()
            .Where(u => u.IsActive && u.Status == ItemStatus.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, ItemStatus.Active));

        if (fixedProducts > 0 || fixedUnits > 0)
            _logger.LogInformation("Fixed status: {Products} products, {Units} units corrected from Draft→Active.", fixedProducts, fixedUnits);
    }
}
