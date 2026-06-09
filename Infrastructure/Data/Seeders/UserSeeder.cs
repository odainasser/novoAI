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

}
