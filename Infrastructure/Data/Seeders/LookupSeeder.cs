using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class LookupSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LookupSeeder> _logger;

    public LookupSeeder(ApplicationDbContext context, ILogger<LookupSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Seed root (parent) lookups first
        await SeedRootLookupsAsync();

        // Then seed children under each root
        await SeedWarehouseTypesAsync();
        await SeedDeviceTypesAsync();
        await SeedUnitsOfMeasureAsync();
        await SeedUnitTypesAsync();
    }

    private async Task SeedRootLookupsAsync()
    {
        var roots = new List<Lookup>
        {
            new() { Code = "WAREHOUSE_TYPE",        NameEn = "Warehouse Type",       NameAr = "نوع المستودع",        IsActive = true },
            new() { Code = "DEVICE_TYPE",           NameEn = "Device Type",          NameAr = "نوع الجهاز",          IsActive = true },
            new() { Code = "UNIT_OF_MEASURE",       NameEn = "Unit of Measure",      NameAr = "وحدة القياس",         IsActive = true },
            new() { Code = "UNIT_TYPE",             NameEn = "Unit Type",            NameAr = "نوع الوحدة",          IsActive = true },
        };

        foreach (var root in roots)
        {
            var exists = await _context.Lookups
                .IgnoreQueryFilters()
                .AnyAsync(l => l.Code == root.Code && l.ParentId == null);

            if (!exists)
            {
                _context.Lookups.Add(root);
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Root lookups seeded.");
    }

    private async Task SeedWarehouseTypesAsync()
    {
        var parent = await GetRootAsync("WAREHOUSE_TYPE");
        if (parent == null) return;

        var lookups = new List<Lookup>
        {
            new() { Code = "CW",  NameEn = "Central Warehouse",         NameAr = "المستودع المركزي",      ParentId = parent.Id, IsActive = true },
            new() { Code = "MS",  NameEn = "Branch Store",              NameAr = "متجر المتحف",           ParentId = parent.Id, IsActive = true },
            new() { Code = "MW",  NameEn = "Branch Warehouse",          NameAr = "مستودع المتحف",         ParentId = parent.Id, IsActive = true },
        };

        await SeedChildrenAsync(lookups, parent);
    }

    private async Task SeedDeviceTypesAsync()
    {
        var parent = await GetRootAsync("DEVICE_TYPE");
        if (parent == null) return;

        var lookups = new List<Lookup>
        {
            new() { Code = "PRINTER",  NameEn = "Printer",  NameAr = "طابعة",  ParentId = parent.Id, IsActive = true },
            new() { Code = "SCANNER",  NameEn = "Scanner",  NameAr = "ماسح ضوئي",  ParentId = parent.Id, IsActive = true },
        };

        await SeedChildrenAsync(lookups, parent);
    }

    private async Task SeedUnitsOfMeasureAsync()
    {
        var parent = await GetRootAsync("UNIT_OF_MEASURE");
        if (parent == null) return;

        var lookups = new List<Lookup>
        {
            new() { Code = "UOM_PIECE",  NameEn = "Piece",  NameAr = "قطعة",   ParentId = parent.Id, IsActive = true },
            new() { Code = "UOM_BOX",    NameEn = "Box",    NameAr = "صندوق",   ParentId = parent.Id, IsActive = true },
            new() { Code = "UOM_PACK",   NameEn = "Pack",   NameAr = "حزمة",    ParentId = parent.Id, IsActive = true },
            new() { Code = "UOM_SET",    NameEn = "Set",    NameAr = "طقم",     ParentId = parent.Id, IsActive = true },
            new() { Code = "UOM_PAIR",   NameEn = "Pair",   NameAr = "زوج",     ParentId = parent.Id, IsActive = true },
        };

        await SeedChildrenAsync(lookups, parent);
    }

    private async Task SeedUnitTypesAsync()
    {
        var parent = await GetRootAsync("UNIT_TYPE");
        if (parent == null) return;

        var lookups = new List<Lookup>
        {
            new() { Code = "UT_SELLING",   NameEn = "Selling",   NameAr = "بيع",       ParentId = parent.Id, IsActive = true },
            new() { Code = "UT_LOGISTICS", NameEn = "Logistics", NameAr = "لوجستيات", ParentId = parent.Id, IsActive = true },
        };

        await SeedChildrenAsync(lookups, parent);
    }

    private async Task<Lookup?> GetRootAsync(string code)
    {
        return await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == code && l.ParentId == null);
    }

    private async Task SeedChildrenAsync(List<Lookup> lookups, Lookup parent)
    {
        var existingCodes = await _context.Lookups
            .IgnoreQueryFilters()
            .Where(l => l.ParentId == parent.Id)
            .Select(l => l.Code)
            .ToListAsync();

        var newLookups = lookups.Where(l => !existingCodes.Contains(l.Code)).ToList();

        if (newLookups.Count == 0)
        {
            _logger.LogInformation("All {Parent} children already exist. Skipping.", parent.Code);
            return;
        }

        await _context.Lookups.AddRangeAsync(newLookups);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} children under {Parent}.", newLookups.Count, parent.Code);
    }
}
