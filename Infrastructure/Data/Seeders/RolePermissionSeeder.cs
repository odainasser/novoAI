using Domain.Constants;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class RolePermissionSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RolePermissionSeeder> _logger;

    public RolePermissionSeeder(
        ApplicationDbContext context,
        ILogger<RolePermissionSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting role-permission seeding...");

        // Get roles from Domain entities
        var adminRole = await _context.DomainRoles
            .FirstOrDefaultAsync(r => r.Name == Roles.Administrator);
        var cashierRole = await _context.DomainRoles
            .FirstOrDefaultAsync(r => r.Name == Roles.Cashier);
        var branchManagerRole = await _context.DomainRoles
            .FirstOrDefaultAsync(r => r.Name == Roles.BranchManager);

        // Create roles if they don't exist
        if (adminRole == null)
        {
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = Roles.Administrator,
                DescriptionEn = "Full system access with all permissions",
                DescriptionAr = "\u0635\u0644\u0627\u062d\u064a\u0627\u062a \u0643\u0627\u0645\u0644\u0629 \u0644\u0644\u0646\u0638\u0627\u0645",
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.DomainRoles.Add(adminRole);
        }
        else if (!adminRole.IsSystemRole)
        {
            adminRole.IsSystemRole = true;
            _context.Entry(adminRole).Property(r => r.IsSystemRole).IsModified = true;
        }

        if (cashierRole == null)
        {
            cashierRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = Roles.Cashier,
                DescriptionEn = "Point of sale cashier with transaction access",
                DescriptionAr = "\u0623\u0645\u064a\u0646 \u0627\u0644\u0635\u0646\u062f\u0648\u0642",
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.DomainRoles.Add(cashierRole);
        }
        else if (!cashierRole.IsSystemRole)
        {
            cashierRole.IsSystemRole = true;
            _context.Entry(cashierRole).Property(r => r.IsSystemRole).IsModified = true;
        }

        if (branchManagerRole == null)
        {
            branchManagerRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = Roles.BranchManager,
                DescriptionEn = "Branch-scoped access to orders, inventory, requests, and shifts",
                DescriptionAr = "\u0648\u0635\u0648\u0644 \u0645\u062d\u062f\u0648\u062f \u0628\u0627\u0644\u0641\u0631\u0639 \u0644\u0644\u0637\u0644\u0628\u0627\u062a \u0648\u0627\u0644\u0645\u062e\u0632\u0648\u0646",
                IsSystemRole = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.DomainRoles.Add(branchManagerRole);
        }
        else if (!branchManagerRole.IsSystemRole)
        {
            branchManagerRole.IsSystemRole = true;
            _context.Entry(branchManagerRole).Property(r => r.IsSystemRole).IsModified = true;
        }

        await _context.SaveChangesAsync();

        // Get all permissions
        var allPermissions = await _context.Permissions.ToListAsync();

        // Administrator - All permissions
        await AssignPermissionsToRoleAsync(adminRole, allPermissions);

        // Cashier - Read permissions for products and categories (for POS), and order permissions
        var cashierPermissions = allPermissions
            .Where(p => p.Code == Permissions.ProductsRead ||
                        p.Code == Permissions.CategoriesRead ||
                        p.Code == Permissions.OrdersWrite ||
                        p.Code == Permissions.OrdersReadOwn ||
                        p.Code == Permissions.ShiftsWrite ||
                        p.Code == Permissions.ShiftsReadOwn ||
                        p.Code == Permissions.RequestsWrite)
            .ToList();

        _logger.LogInformation("Assigning {Count} permissions to Cashier role: {Permissions}",
            cashierPermissions.Count,
            string.Join(", ", cashierPermissions.Select(p => p.Code)));

        await AssignPermissionsToRoleAsync(cashierRole, cashierPermissions);

        // BranchManager — read access to every section in the Branch Panel,
        // plus the ability to submit (write) requests. Membership in
        // UserBranches scopes the actual data the user sees; permissions only
        // gate which sidebar sections are visible.
        var branchManagerPermissions = allPermissions
            .Where(p => p.Code == Permissions.OrdersRead ||
                        p.Code == Permissions.InventoryRead ||
                        // Stocktake (physical count) is owned end-to-end by the branch
                        // manager: create/count (write) and review/approve in their branch.
                        p.Code == Permissions.InventoryWrite ||
                        p.Code == Permissions.InventoryApprove ||
                        // Purchase requests: branch managers raise and track them
                        // (approval/conversion remain with Admin-Panel approvers).
                        p.Code == Permissions.PurchaseRequestsRead ||
                        p.Code == Permissions.PurchaseRequestsWrite ||
                        p.Code == Permissions.RequestsRead ||
                        p.Code == Permissions.RequestsWrite ||
                        p.Code == Permissions.ShiftsRead ||
                        p.Code == Permissions.CashiersRead ||
                        p.Code == Permissions.WarehousesRead ||
                        p.Code == Permissions.UnitsRead ||
                        p.Code == Permissions.UnitsPrice ||
                        p.Code == Permissions.UnitsLogistics ||
                        p.Code == Permissions.SuppliersRead ||
                        p.Code == Permissions.BranchesRead)
            .ToList();

        _logger.LogInformation("Assigning {Count} permissions to BranchManager role: {Permissions}",
            branchManagerPermissions.Count,
            string.Join(", ", branchManagerPermissions.Select(p => p.Code)));

        await AssignPermissionsToRoleAsync(branchManagerRole, branchManagerPermissions);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Role-permission seeding completed");
    }

    private async Task AssignPermissionsToRoleAsync(Role role, List<Permission> permissions)
    {
        foreach (var permission in permissions)
        {
            var exists = await _context.RolePermissions
                .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);

            if (!exists)
            {
                var rolePermission = new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = "System"
                };

                _context.RolePermissions.Add(rolePermission);
                _logger.LogInformation("Assigned permission '{Permission}' to role '{Role}'",
                    permission.Code, role.Name);
            }
        }
    }
}
