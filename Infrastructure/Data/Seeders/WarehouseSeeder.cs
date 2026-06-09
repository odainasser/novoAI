using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class WarehouseSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WarehouseSeeder> _logger;

    public WarehouseSeeder(ApplicationDbContext context, ILogger<WarehouseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting warehouse seeding...");

        var warehouseTypeRoot = await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "WAREHOUSE_TYPE" && l.ParentId == null);

        if (warehouseTypeRoot == null)
        {
            _logger.LogWarning("WAREHOUSE_TYPE root lookup not found. Skipping warehouse seeding.");
            return;
        }

        await SeedCentralWarehouseAsync(warehouseTypeRoot.Id);
        await SeedBranchStoreWarehousesAsync(warehouseTypeRoot.Id);
    }

    private async Task SeedCentralWarehouseAsync(Guid warehouseTypeRootId)
    {
        var centralType = await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "CW" && l.ParentId == warehouseTypeRootId);

        if (centralType == null)
        {
            _logger.LogWarning("Central Warehouse (CW) type not found. Skipping.");
            return;
        }

        var exists = await _context.Warehouses
            .IgnoreQueryFilters()
            .AnyAsync(w => w.WarehouseTypeId == centralType.Id);

        if (exists)
        {
            _logger.LogInformation("Central warehouse already exists. Skipping.");
            return;
        }

        var centralWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            NameEn = "ByteMart Central Warehouse",
            NameAr = "المستودع المركزي لبايت مارت",
            Address = "Industrial Area 12, Sharjah, UAE",
            ContactPerson = "Warehouse Manager",
            ContactPhone = "+971-6-555-0000",
            Email = "warehouse@bytemart.ae",
            WarehouseTypeId = centralType.Id,
            BranchId = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        _context.Warehouses.Add(centralWarehouse);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded central warehouse.");
    }

    private async Task SeedBranchStoreWarehousesAsync(Guid warehouseTypeRootId)
    {
        var branchStoreType = await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "MS" && l.ParentId == warehouseTypeRootId);

        if (branchStoreType == null)
        {
            _logger.LogWarning("Branch Store (MS) warehouse type not found. Skipping branch store seeding.");
            return;
        }

        var branches = await _context.Branches
            .IgnoreQueryFilters()
            .Where(m => m.IsActive)
            .ToListAsync();

        if (branches.Count == 0)
        {
            _logger.LogInformation("No branches found. Skipping branch store seeding.");
            return;
        }

        // Get existing branch store warehouses to avoid duplicates
        var existingBranchIds = await _context.Warehouses
            .IgnoreQueryFilters()
            .Where(w => w.WarehouseTypeId == branchStoreType.Id && w.BranchId != null)
            .Select(w => w.BranchId!.Value)
            .ToListAsync();

        var newWarehouses = new List<Warehouse>();

        foreach (var branch in branches)
        {
            if (existingBranchIds.Contains(branch.Id))
                continue;

            newWarehouses.Add(new Warehouse
            {
                Id = Guid.NewGuid(),
                NameEn = $"{branch.NameEn} - Store Stockroom",
                NameAr = $"مخزن {branch.NameAr}",
                Address = branch.NameEn,
                WarehouseTypeId = branchStoreType.Id,
                BranchId = branch.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            });
        }

        if (newWarehouses.Count == 0)
        {
            _logger.LogInformation("All branch store warehouses already exist. Skipping.");
            return;
        }

        await _context.Warehouses.AddRangeAsync(newWarehouses);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} branch store warehouses.", newWarehouses.Count);
    }
}
