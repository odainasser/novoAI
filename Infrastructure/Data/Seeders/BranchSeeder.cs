using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class BranchSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BranchSeeder> _logger;

    public BranchSeeder(ApplicationDbContext context, ILogger<BranchSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting branch seeding...");

        // NOTE: The "Branch" entity is reused here as a store-branch grouping for the
        // market dataset. Schema name unchanged; only the seeded data describes branches.
        var branches = new List<Branch>
        {
            new() { NameEn = "ByteMart Downtown",          NameAr = "بايت مارت وسط المدينة",        IsActive = true },
            new() { NameEn = "ByteMart City Centre",       NameAr = "بايت مارت سيتي سنتر",          IsActive = true },
            new() { NameEn = "ByteMart Al Nahda",          NameAr = "بايت مارت النهدة",             IsActive = true },
            new() { NameEn = "ByteMart Al Majaz",          NameAr = "بايت مارت المجاز",             IsActive = true },
            new() { NameEn = "ByteMart Al Khan",           NameAr = "بايت مارت الخان",              IsActive = true },
            new() { NameEn = "ByteMart Muweilah Express",  NameAr = "بايت مارت المويلح إكسبرس",     IsActive = true },
            new() { NameEn = "ByteMart Industrial Area",   NameAr = "بايت مارت المنطقة الصناعية",   IsActive = true },
            new() { NameEn = "ByteMart Al Qasimia",        NameAr = "بايت مارت القاسمية",           IsActive = true },
            new() { NameEn = "ByteMart Al Buhairah",       NameAr = "بايت مارت البحيرة",            IsActive = true },
            new() { NameEn = "ByteMart Mega Mall",         NameAr = "بايت مارت ميجا مول",           IsActive = true },
            new() { NameEn = "ByteMart Al Taawun",         NameAr = "بايت مارت التعاون",            IsActive = true },
            new() { NameEn = "ByteMart Corniche",          NameAr = "بايت مارت الكورنيش",           IsActive = true },
        };

        var existingNames = await _context.Branches
            .IgnoreQueryFilters()
            .Select(m => m.NameEn)
            .ToListAsync();

        var newBranches = branches.Where(m => !existingNames.Contains(m.NameEn)).ToList();

        if (newBranches.Count == 0)
        {
            _logger.LogInformation("All branches already exist. Skipping.");
            return;
        }

        await _context.Branches.AddRangeAsync(newBranches);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} branches.", newBranches.Count);
    }
}
