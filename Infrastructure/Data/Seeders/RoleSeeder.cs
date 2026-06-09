using Domain.Constants;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data.Seeders;

public class RoleSeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<RoleSeeder> _logger;

    public RoleSeeder(
        RoleManager<ApplicationRole> roleManager,
        ILogger<RoleSeeder> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting role seeding...");

        var roles = new List<(string Name, string NameAr, string DescriptionEn, string DescriptionAr)>
        {
            (Roles.Administrator, "\u0645\u0633\u0624\u0648\u0644 \u0627\u0644\u0646\u0638\u0627\u0645", "Full system access with all permissions", "\u0635\u0644\u0627\u062d\u064a\u0627\u062a \u0643\u0627\u0645\u0644\u0629 \u0644\u0644\u0646\u0638\u0627\u0645"),
            (Roles.BranchManager, "\u0645\u062f\u064a\u0631 \u0641\u0631\u0639", "Branch-scoped access to orders, inventory, and requests", "\u0648\u0635\u0648\u0644 \u0645\u062d\u062f\u0648\u062f \u0628\u0627\u0644\u0641\u0631\u0639 \u0644\u0644\u0637\u0644\u0628\u0627\u062a \u0648\u0627\u0644\u0645\u062e\u0632\u0648\u0646")
        };

        foreach (var (name, nameAr, descriptionEn, descriptionAr) in roles)
        {
            if (!await _roleManager.RoleExistsAsync(name))
            {
                var role = new ApplicationRole
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    NameAr = nameAr,
                    NormalizedName = name.ToUpper(),
                    DescriptionEn = descriptionEn,
                    DescriptionAr = descriptionAr,
                    IsSystemRole = true
                };

                var result = await _roleManager.CreateAsync(role);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Role '{RoleName}' created successfully", name);
                }
                else
                {
                    _logger.LogError("Failed to create role '{RoleName}': {Errors}",
                        name, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                var existingRole = await _roleManager.FindByNameAsync(name);
                if (existingRole != null)
                {
                    bool changed = false;
                    if (string.IsNullOrEmpty(existingRole.NameAr) && !string.IsNullOrEmpty(nameAr))
                    {
                        existingRole.NameAr = nameAr;
                        changed = true;
                    }
                    if (string.IsNullOrEmpty(existingRole.DescriptionEn) && !string.IsNullOrEmpty(descriptionEn))
                    {
                        existingRole.DescriptionEn = descriptionEn;
                        changed = true;
                    }
                    if (string.IsNullOrEmpty(existingRole.DescriptionAr) && !string.IsNullOrEmpty(descriptionAr))
                    {
                        existingRole.DescriptionAr = descriptionAr;
                        changed = true;
                    }
                    if (!existingRole.IsSystemRole)
                    {
                        existingRole.IsSystemRole = true;
                        changed = true;
                    }

                    if (changed)
                    {
                        await _roleManager.UpdateAsync(existingRole);
                        _logger.LogInformation("Role '{RoleName}' updated with details", name);
                    }
                    else
                    {
                        _logger.LogInformation("Role '{RoleName}' already exists and is up to date", name);
                    }
                }
            }
        }

        _logger.LogInformation("Role seeding completed");
    }
}
