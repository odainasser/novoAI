using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class DatabaseSeeder
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IServiceProvider serviceProvider,
        ILogger<DatabaseSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting database seeding...");

        using var scope = _serviceProvider.CreateScope();
        var services = scope.ServiceProvider;

        try
        {
            // Seed in order: Roles -> Users -> Permissions -> RolePermissions -> Lookups

            // 1. Seed Roles (Identity)
            var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var roleSeederLogger = services.GetRequiredService<ILogger<RoleSeeder>>();
            var roleSeeder = new RoleSeeder(roleManager, roleSeederLogger);
            await roleSeeder.SeedAsync();

            // 2. Seed Branches (Domain)
            var context = services.GetRequiredService<ApplicationDbContext>();
            var branchSeederLogger = services.GetRequiredService<ILogger<BranchSeeder>>();
            var branchSeeder = new BranchSeeder(context, branchSeederLogger);
            await branchSeeder.SeedAsync();

            // 3. Seed Users (Identity)
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var userSeederLogger = services.GetRequiredService<ILogger<UserSeeder>>();
            var userSeeder = new UserSeeder(userManager, context, userSeederLogger);
            await userSeeder.SeedAsync();

            // 4. Seed Permissions (Domain)
            var permissionSeederLogger = services.GetRequiredService<ILogger<PermissionSeeder>>();
            var permissionSeeder = new PermissionSeeder(context, permissionSeederLogger);
            await permissionSeeder.SeedAsync();

            // 5. Seed Role-Permission mappings (Domain)
            var rolePermissionSeederLogger = services.GetRequiredService<ILogger<RolePermissionSeeder>>();
            var rolePermissionSeeder = new RolePermissionSeeder(context, rolePermissionSeederLogger);
            await rolePermissionSeeder.SeedAsync();

            // 6. Seed Lookups (Warehouse types, Transfer types, etc.)
            var lookupSeederLogger = services.GetRequiredService<ILogger<LookupSeeder>>();
            var lookupSeeder = new LookupSeeder(context, lookupSeederLogger);
            await lookupSeeder.SeedAsync();

            // 7. Seed Branch Store Warehouses (one per branch)
            var warehouseSeederLogger = services.GetRequiredService<ILogger<WarehouseSeeder>>();
            var warehouseSeeder = new WarehouseSeeder(context, warehouseSeederLogger);
            await warehouseSeeder.SeedAsync();

            // 7.6 Assign every active branch to the seeded BranchManager user
            // via UserBranches. Branches are already seeded in step 2.
            await userSeeder.AssignBranchManagerBranchesAsync();

            // 8. Seed Suppliers (must be before catalog so unit supplier barcodes can reference them)
            var supplierSeederLogger = services.GetRequiredService<ILogger<SupplierSeeder>>();
            var supplierSeeder = new SupplierSeeder(context, supplierSeederLogger);
            await supplierSeeder.SeedAsync();

            // 9. Seed Catalog (categories, gift products, units, stock balances)
            var catalogSeederLogger = services.GetRequiredService<ILogger<CatalogSeeder>>();
            var catalogSeeder = new CatalogSeeder(context, catalogSeederLogger);
            await catalogSeeder.SeedAsync();

            // The tool-calling assistant needs no seed data — its tools are
            // code-owned, and the model understands natural language directly.

            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }
}
