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

        await _context.SaveChangesAsync();

        // Get all permissions
        var allPermissions = await _context.Permissions.ToListAsync();

        // Administrator - All permissions
        await AssignPermissionsToRoleAsync(adminRole, allPermissions);

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
