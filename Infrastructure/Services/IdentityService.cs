using Application.Common.Interfaces;
using Domain.Entities;
using Infrastructure.Identity;
using Infrastructure.Mapping;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Application.Features.UserLogs;
using Domain.Enums;
using Application.Services;

namespace Infrastructure.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext context,
        IUserLogService userLogService,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _context = context;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
    }

    #region Existing Methods

    public async Task<(bool Success, string[] Errors, User? User)> CreateUserAsync(User user, string password)
    {
        var identityUser = UserMapper.ToIdentityUser(user);
        var result = await _userManager.CreateAsync(identityUser, password);

        if (!result.Succeeded)
        {
            return (false, result.Errors.Select(e => e.Description).ToArray(), null);
        }

        var createdUser = UserMapper.ToDomainUser(identityUser);

        // Log creation
        var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
        if (actorId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = actorId,
                UserName = actorName,
                Action = AuditAction.Created,
                EntityName = "User",
                EntityId = createdUser.Id.ToString(),
                Details = null
            });
        }

        return (true, Array.Empty<string>(), createdUser);
    }

    public async Task<(bool Success, string Message, User? User)> ValidateCredentialsAsync(string email, string password)
    {
        var identityUser = await _userManager.FindByEmailAsync(email);
        if (identityUser == null)
        {
            return (false, "Invalid email or password", null);
        }

        // First validate credentials (this handles lockout on failure)
        var result = await _signInManager.CheckPasswordSignInAsync(identityUser, password, lockoutOnFailure: true);

        // If locked out as a result of failed attempts
        if (result.IsLockedOut)
        {
            return (false, "Account locked due to multiple failed login attempts", null);
        }

        // If password incorrect
        if (!result.Succeeded)
        {
            return (false, "Invalid email or password", null);
        }

        // At this point credentials are valid — map to domain user and check active flag
        var domainUser = UserMapper.ToDomainUser(identityUser);

        if (!domainUser.IsActive)
        {
            return (false, "UserAccountDeactivated", null);
        }

        // Ensure none of the assigned roles are deactivated
        var roleNames = await _userManager.GetRolesAsync(identityUser);
        foreach (var roleName in roleNames)
        {
            var appRole = await _roleManager.FindByNameAsync(roleName);
            if (appRole != null && !appRole.IsActive)
            {
                // Return same message as deactivated user to avoid revealing role details
                return (false, "User account is deactivated", null);
            }
        }

        return (true, "Login successful", domainUser);
    }

    public async Task<User?> FindByEmailAsync(string email)
    {
        var identityUser = await _userManager.FindByEmailAsync(email);
        return identityUser != null ? UserMapper.ToDomainUser(identityUser) : null;
    }

    public async Task<User?> FindByIdAsync(Guid userId)
    {
        var identityUser = await _userManager.FindByIdAsync(userId.ToString());
        return identityUser != null ? UserMapper.ToDomainUser(identityUser) : null;
    }

    public async Task<IList<string>> GetUserRolesAsync(Guid userId)
    {
        var identityUser = await _userManager.FindByIdAsync(userId.ToString());
        if (identityUser == null)
        {
            return new List<string>();
        }

        return await _userManager.GetRolesAsync(identityUser);
    }

    public async Task<bool> CheckPasswordAsync(User user, string password)
    {
        var identityUser = await _userManager.FindByIdAsync(user.Id.ToString());
        if (identityUser == null)
        {
            return false;
        }

        return await _userManager.CheckPasswordAsync(identityUser, password);
    }

    #endregion

    #region Email Confirmation

    public async Task<string> GenerateEmailConfirmationTokenAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        return await _userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<(bool Success, string[] Errors)> ConfirmEmailAsync(Guid userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (result.Succeeded)
        {
            // Log email confirmed
            var domainUser = UserMapper.ToDomainUser(user);
            var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
            if (actorId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = actorId,
                    UserName = actorName,
                    Action = AuditAction.EmailVerified,
                    EntityName = "User",
                    EntityId = domainUser.Id.ToString(),
                    Details = null
                });
            }
        }

        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<bool> IsEmailConfirmedAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        return await _userManager.IsEmailConfirmedAsync(user);
    }

    #endregion

    #region Password Management

    public async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<(bool Success, string[] Errors)> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        
        if (result.Succeeded)
        {
            // Note: ResetPasswordAsync already updates the security stamp internally

            // Log password reset
            var domainUser = UserMapper.ToDomainUser(user);
            var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
            if (actorId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = actorId,
                    UserName = actorName,
                    Action = AuditAction.PasswordReset,
                    EntityName = "User",
                    EntityId = domainUser.Id.ToString(),
                    Details = null
                });
            }
        }
        
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        
        if (result.Succeeded)
        {
            // Update security stamp to invalidate existing tokens
            await _userManager.UpdateSecurityStampAsync(user);

            // Log password changed
            var domainUser = UserMapper.ToDomainUser(user);
            var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
            if (actorId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = actorId,
                    UserName = actorName,
                    Action = AuditAction.PasswordChanged,
                    EntityName = "User",
                    EntityId = domainUser.Id.ToString(),
                    Details = null
                });
            }
        }
        
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    #endregion

    #region Account Lockout

    public async Task<(bool Success, string Message)> AccessFailedAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, "User not found");
        }

        var result = await _userManager.AccessFailedAsync(user);
        
        if (result.Succeeded)
        {
            if (await _userManager.IsLockedOutAsync(user))
            {
                return (true, "Account has been locked due to multiple failed login attempts");
            }
            
            return (true, "Access failed recorded");
        }

        return (false, "Failed to record access failure");
    }

    public async Task ResetAccessFailedCountAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }
    }

    public async Task<bool> IsLockedOutAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        return await _userManager.IsLockedOutAsync(user);
    }

    public async Task<(bool Success, string[] Errors)> SetLockoutEndDateAsync(Guid userId, DateTimeOffset? lockoutEnd)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> SetLockoutEnabledAsync(Guid userId, bool enabled)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.SetLockoutEnabledAsync(user, enabled);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    #endregion

    #region User Management

    public async Task<(bool Success, string[] Errors)> UpdateUserAsync(User user)
    {
        var identityUser = await _userManager.FindByIdAsync(user.Id.ToString());
        if (identityUser == null)
        {
            return (false, new[] { "User not found" });
        }

        UserMapper.UpdateIdentityUser(identityUser, user);
        var result = await _userManager.UpdateAsync(identityUser);
        
        if (result.Succeeded)
        {
            var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
            if (actorId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = actorId,
                    UserName = actorName,
                    Action = AuditAction.Updated,
                    EntityName = "User",
                    EntityId = user.Id.ToString(),
                    Details = null
                });
            }
        }
        
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> DeleteUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        // Soft delete
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;

        var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
        if (!string.IsNullOrEmpty(actorName))
        {
            user.DeletedBy = actorName;
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            if (actorId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = actorId,
                    UserName = actorName,
                    Action = AuditAction.Deleted,
                    EntityName = "User",
                    EntityId = user.Id.ToString(),
                    Details = null
                });
            }
        }
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> AddToRoleAsync(Guid userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (result.Succeeded)
        {
            var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
            if (actorId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = actorId,
                    UserName = actorName,
                    Action = AuditAction.Updated,
                    EntityName = "User",
                    EntityId = user.Id.ToString(),
                    Details = null
                });
            }
        }

        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> RemoveFromRoleAsync(Guid userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (result.Succeeded)
        {
            var (actorId, actorName) = await _currentUserService.GetCurrentUserAsync();
            if (actorId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = actorId,
                    UserName = actorName,
                    Action = AuditAction.Updated,
                    EntityName = "User",
                    EntityId = user.Id.ToString(),
                    Details = null
                });
            }
        }

        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<bool> IsInRoleAsync(Guid userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        return await _userManager.IsInRoleAsync(user, roleName);
    }

    #endregion

    #region Phone Number

    public async Task<string> GenerateChangePhoneNumberTokenAsync(Guid userId, string phoneNumber)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        return await _userManager.GenerateChangePhoneNumberTokenAsync(user, phoneNumber);
    }

    public async Task<(bool Success, string[] Errors)> ChangePhoneNumberAsync(Guid userId, string phoneNumber, string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.ChangePhoneNumberAsync(user, phoneNumber, token);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> SetPhoneNumberAsync(Guid userId, string phoneNumber)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return (false, new[] { "User not found" });
        }

        var result = await _userManager.SetPhoneNumberAsync(user, phoneNumber);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    #endregion

    #region Security

    public async Task UpdateSecurityStampAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user != null)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }
    }

    public async Task<string> GenerateUserTokenAsync(Guid userId, string purpose)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        return await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, purpose);
    }

    public async Task<bool> VerifyUserTokenAsync(Guid userId, string purpose, string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        return await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, purpose, token);
    }

    #endregion

    #region Permissions

    public async Task<IList<string>> GetUserPermissionsAsync(Guid userId)
    {
        // Get role names from ASP.NET Identity
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.IsActive)
        {
            return new List<string>();
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        if (!roleNames.Any())
        {
            return new List<string>();
        }

        // Get permissions from Domain Roles based on role names
        var permissions = await _context.DomainRoles
            .Where(r => roleNames.Contains(r.Name))
            .SelectMany(r => r.RolePermissions
                .Select(rp => rp.Permission.Code))
            .Distinct()
            .ToListAsync();

        return permissions;
    }

    #endregion
}

