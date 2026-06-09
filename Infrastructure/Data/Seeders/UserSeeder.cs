using Domain.Constants;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class UserSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserSeeder> _logger;
    private const string DefaultPassword = "ByteArabia@123!";
    public UserSeeder(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<UserSeeder> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting user seeding...");

        await SeedAdminUserAsync();
        await SeedDefaultUsersAsync();
        await SeedCashierUserAsync();
        await SeedBranchManagerUserAsync();

        _logger.LogInformation("User seeding completed");
    }

    private async Task SeedAdminUserAsync()
    {
        var adminEmail = "admin@bytearabia.tech";

        var existingAdmin = await _userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin != null)
        {
            if (!existingAdmin.IsSystemUser)
            {
                existingAdmin.IsSystemUser = true;
                await _userManager.UpdateAsync(existingAdmin);
                _logger.LogInformation("Updated IsSystemUser for Admin user");
            }
            _logger.LogInformation("Admin user already exists");
            return;
        }

        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = adminEmail,
            Email = adminEmail,
            NormalizedUserName = adminEmail.ToUpper(),
            NormalizedEmail = adminEmail.ToUpper(),
            FirstName = "System",
            LastName = "Administrator",
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            IsSystemUser = true
        };

        var result = await _userManager.CreateAsync(adminUser, DefaultPassword);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(adminUser, Roles.Administrator);
            _logger.LogInformation("Admin user created successfully with email: {Email}", adminEmail);
        }
        else
        {
            _logger.LogError("Failed to create admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedDefaultUsersAsync()
    {
        // Removed default users (Manager, Support, User)
        await Task.CompletedTask;
    }

    private async Task SeedCashierUserAsync()
    {
        var cashierEmail = "cashier@bytearabia.tech";

        var existingCashier = await _userManager.FindByEmailAsync(cashierEmail);
        if (existingCashier != null)
        {
            if (!existingCashier.IsSystemUser)
            {
                existingCashier.IsSystemUser = true;
                await _userManager.UpdateAsync(existingCashier);
                _logger.LogInformation("Updated IsSystemUser for Cashier user");
            }
            _logger.LogInformation("Cashier user already exists");
            return;
        }

        var cashierUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = cashierEmail,
            Email = cashierEmail,
            NormalizedUserName = cashierEmail.ToUpper(),
            NormalizedEmail = cashierEmail.ToUpper(),
            FirstName = "Default",
            LastName = "Cashier",
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            IsSystemUser = true,
            WarehouseId = null // Will be assigned after warehouses are seeded
        };

        var result = await _userManager.CreateAsync(cashierUser, DefaultPassword);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(cashierUser, Roles.Cashier);
            _logger.LogInformation("Cashier user created successfully with email: {Email}", cashierEmail);
        }
        else
        {
            _logger.LogError("Failed to create cashier user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedBranchManagerUserAsync()
    {
        var branchManagerEmail = "branch@bytearabia.tech";

        var existingBranchManager = await _userManager.FindByEmailAsync(branchManagerEmail);
        if (existingBranchManager != null)
        {
            if (!existingBranchManager.IsSystemUser)
            {
                existingBranchManager.IsSystemUser = true;
                await _userManager.UpdateAsync(existingBranchManager);
                _logger.LogInformation("Updated IsSystemUser for BranchManager user");
            }
            _logger.LogInformation("BranchManager user already exists");
            return;
        }

        var branchManagerUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = branchManagerEmail,
            Email = branchManagerEmail,
            NormalizedUserName = branchManagerEmail.ToUpper(),
            NormalizedEmail = branchManagerEmail.ToUpper(),
            FirstName = "Default",
            LastName = "Branch Manager",
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            IsSystemUser = true
        };

        var result = await _userManager.CreateAsync(branchManagerUser, DefaultPassword);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(branchManagerUser, Roles.BranchManager);
            _logger.LogInformation("BranchManager user created successfully with email: {Email}", branchManagerEmail);
        }
        else
        {
            _logger.LogError("Failed to create BranchManager user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    /// <summary>
    /// Assigns every active branch to the seeded BranchManager user via
    /// UserBranches. Mirrors the Cashier→Stores pattern. Called by
    /// DatabaseSeeder after the branch seeder has run.
    /// </summary>
    public async Task AssignBranchManagerBranchesAsync()
    {
        var branchManagerEmail = "branch@bytearabia.tech";
        var branchManagerUser = await _userManager.FindByEmailAsync(branchManagerEmail);
        if (branchManagerUser == null)
        {
            _logger.LogWarning("BranchManager user not found. Skipping branch assignment.");
            return;
        }

        var existingAssignments = await _context.UserBranches
            .Where(ub => ub.UserId == branchManagerUser.Id)
            .AnyAsync();

        if (existingAssignments)
        {
            _logger.LogInformation("BranchManager already has branch assignments. Skipping.");
            return;
        }

        var branches = await _context.Branches
            .IgnoreQueryFilters()
            .Where(b => b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.NameEn)
            .ToListAsync();

        if (!branches.Any())
        {
            _logger.LogInformation("No active branches found. Skipping BranchManager branch assignment.");
            return;
        }

        foreach (var branch in branches)
        {
            _context.UserBranches.Add(new Domain.Entities.UserBranch
            {
                UserId = branchManagerUser.Id,
                BranchId = branch.Id
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Assigned {Count} branches to BranchManager user.", branches.Count);
    }

    /// <summary>
    /// Assigns all branch store warehouses to the seeded cashier user.
    /// Must be called after warehouses have been seeded.
    /// </summary>
    public async Task AssignCashierStoresAsync()
    {
        var cashierEmail = "cashier@bytearabia.tech";
        var cashierUser = await _userManager.FindByEmailAsync(cashierEmail);
        if (cashierUser == null)
        {
            _logger.LogWarning("Cashier user not found. Skipping store assignment.");
            return;
        }

        // Check if already assigned
        var existingAssignments = await _context.CashierWarehouses
            .Where(cw => cw.CashierId == cashierUser.Id)
            .AnyAsync();

        if (existingAssignments)
        {
            _logger.LogInformation("Cashier already has store assignments. Skipping.");
            return;
        }

        // Find all branch store warehouses (type MS)
        var warehouseTypeRoot = await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "WAREHOUSE_TYPE" && l.ParentId == null);

        if (warehouseTypeRoot == null) return;

        var branchStoreType = await _context.Lookups
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Code == "MS" && l.ParentId == warehouseTypeRoot.Id);

        if (branchStoreType == null) return;

        var storeWarehouses = await _context.Warehouses
            .IgnoreQueryFilters()
            .Where(w => w.WarehouseTypeId == branchStoreType.Id && w.IsActive)
            .OrderBy(w => w.NameEn)
            .ToListAsync();

        if (!storeWarehouses.Any())
        {
            _logger.LogInformation("No branch store warehouses found. Skipping cashier store assignment.");
            return;
        }

        // Assign all stores to the cashier
        foreach (var warehouse in storeWarehouses)
        {
            _context.CashierWarehouses.Add(new Domain.Entities.CashierWarehouse
            {
                CashierId = cashierUser.Id,
                WarehouseId = warehouse.Id
            });
        }

        // Set the first store as the active warehouse
        cashierUser.WarehouseId = storeWarehouses.First().Id;
        await _userManager.UpdateAsync(cashierUser);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Assigned {Count} stores to cashier user.", storeWarehouses.Count);
    }
}
