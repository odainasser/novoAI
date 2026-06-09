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

}
