using Application.Common.Models;
using Application.Features.Users;
using Application.Services;
using Domain.Exceptions;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Domain.Constants;
using Application.Features.UserLogs;
using Domain.Enums;
using Application.Common.Interfaces;
using Application.Common.Behaviors;
using System.Text;

namespace Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaService _mediaService;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;
    private readonly IAppConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IUserLogService userLogService,
        ICurrentUserService currentUserService,
        IMediaService mediaService,
        IIdentityService identityService,
        IEmailService emailService,
        IAppConfiguration configuration,
        ApplicationDbContext dbContext,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
        _mediaService = mediaService;
        _identityService = identityService;
        _emailService = emailService;
        _configuration = configuration;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PaginatedList<UserResponse>> GetAllUsersAsync(int pageNumber, int pageSize, string? role = null, string? search = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = _userManager.Users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchLower)));
        }

        // Apply status filter
        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        // Apply role filter
        if (!string.IsNullOrEmpty(role))
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
            var userIdsInRole = usersInRole.Select(u => u.Id).ToList();
            query = query.Where(u => userIdsInRole.Contains(u.Id));
        }

        var count = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(u => u.UpdatedAt ?? u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        var userResponses = new List<UserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userResponses.Add(await MapToUserResponseAsync(user, roles.ToList()));
        }
        
        return new PaginatedList<UserResponse>(userResponses, count, pageNumber, pageSize);
    }

    public async Task<UserResponse?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);
        return await MapToUserResponseAsync(user, roles.ToList());
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Look up by normalized email including soft-deleted rows. UserManager.FindByEmailAsync
        // honors the IsDeleted query filter and would miss a soft-deleted record, causing the
        // subsequent INSERT to violate Identity's unique NormalizedEmail / NormalizedUserName
        // index and surface as a 500.
        var normalizedEmail = _userManager.NormalizeEmail(request.Email);
        var existingUser = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingUser != null && !existingUser.IsDeleted)
        {
            throw new UserAlreadyExistsException(request.Email);
        }

        if (existingUser != null && existingUser.IsDeleted)
        {
            return await ReactivateUserAsync(existingUser, request, cancellationToken);
        }

        // Generate a random strong password
        var randomPassword = IdentityHelpers.GenerateRandomPassword();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            IsActive = request.IsActive,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, randomPassword);

        if (!result.Succeeded)
        {
            var failures = result.Errors.Select(e => IdentityHelpers.MapIdentityErrorToValidationFailure(e));
            throw new ValidationException(failures);
        }

        if (request.RoleId.HasValue)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId.Value.ToString());
            if (role != null)
            {
                await _userManager.AddToRoleAsync(user, role.Name!);
            }
        }

        // Send welcome email so the user can set their own password
        var emailSent = await SendWelcomeEmailAsync(request.Email);
        if (!emailSent)
        {
            _logger.LogError("Failed to send welcome password setup email to user {Email}", request.Email);
        }

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Created,
                EntityName = "User",
                EntityId = user.Id.ToString(),
                Details = null
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return await MapToUserResponseAsync(user, roles.ToList());
    }

    public async Task<UserResponse> UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new UserNotFoundException($"User with ID '{userId}' not found.");
        }

        // Prevent modifying system users
        if (user.IsSystemUser)
        {
            throw new SystemUserModificationException();
        }

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new UserAlreadyExistsException(request.Email);
            }
            user.Email = request.Email;
            user.UserName = request.Email;
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.IsActive = request.IsActive;
        user.EmailConfirmed = request.EmailConfirmed;
        user.PhoneNumberConfirmed = request.PhoneNumberConfirmed;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Updated,
                EntityName = "User",
                EntityId = user.Id.ToString(),
                Details = null
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return await MapToUserResponseAsync(user, roles.ToList());
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new UserNotFoundException($"User with ID '{userId}' not found.");
        }

        // Prevent deleting system users
        if (user.IsSystemUser)
        {
            throw new SystemUserModificationException();
        }

        // Soft delete
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.IsActive = false;

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (!string.IsNullOrEmpty(currentUserName))
        {
            user.DeletedBy = currentUserName;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Deleted,
                EntityName = "User",
                EntityId = user.Id.ToString(),
                Details = null
            });
        }
    }

    public async Task<UserResponse> AssignRolesToUserAsync(Guid userId, AssignRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new UserNotFoundException($"User with ID '{userId}' not found.");
        }

        // Prevent changing roles for system users who are Administrators
        if (user.IsSystemUser && await _userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            throw new SystemUserModificationException();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        if (request.RoleId.HasValue)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId.Value.ToString());
            if (role != null)
            {
                await _userManager.AddToRoleAsync(user, role.Name!);
            }
        }

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Updated,
                EntityName = "User",
                EntityId = user.Id.ToString(),
                Details = null
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return await MapToUserResponseAsync(user, roles.ToList());
    }

    public async Task<IEnumerable<UserResponse>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users.Where(u => u.IsActive).ToListAsync(cancellationToken);
        
        var userResponses = new List<UserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userResponses.Add(await MapToUserResponseAsync(user, roles.ToList()));
        }
        
        return userResponses;
    }

    private async Task<UserResponse> MapToUserResponseAsync(ApplicationUser user, List<string> roleNames)
    {
        var roleDtos = new List<RoleDto>();
        
        foreach (var roleName in roleNames)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                roleDtos.Add(new RoleDto
                {
                    Id = role.Id,
                    Name = role.Name!,
                    NameEn = role.Name!,
                    NameAr = role.NameAr,
                    DescriptionEn = role.DescriptionEn,
                    DescriptionAr = role.DescriptionAr
                });
            }
        }

        string? avatarUrl = null;
        try
        {
            var mediaList = await _mediaService.GetMediaForEntityAsync(user.Id, EntityType.User, "avatar");
            var avatar = mediaList.FirstOrDefault();
            if (avatar != null)
            {
                avatarUrl = _mediaService.GetMediaUrl(avatar);
            }
        }
        catch
        {
            // Ignore media errors during mapping
        }

        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}",
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd?.DateTime,
            AccessFailedCount = user.AccessFailedCount,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            Roles = roleDtos,
            IsSystemUser = user.IsSystemUser,
            AvatarUrl = avatarUrl
        };
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userManager.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    private async Task<bool> SendWelcomeEmailAsync(string email)
    {
        var token = await _identityService.GeneratePasswordResetTokenAsync(email);
        var appUrl = _configuration.GetAppUrl();

        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var base64UrlToken = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var resetLink = $"{appUrl}/reset-password?email={Uri.EscapeDataString(email)}&token={base64UrlToken}";

        return await _emailService.SendWelcomePasswordSetupAsync(email, resetLink);
    }

    private async Task<UserResponse> ReactivateUserAsync(
        ApplicationUser user,
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        // Restore the soft-deleted account in place so historical references stay intact
        // and the unique email/username index is respected.
        user.IsDeleted = false;
        user.DeletedAt = null;
        user.DeletedBy = null;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.EmailConfirmed = false;
        user.UserName = request.Email;
        user.Email = request.Email;

        // Force a new password so the previous owner's old credentials can't be reused.
        if (await _userManager.HasPasswordAsync(user))
        {
            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
                throw new InvalidOperationException(string.Join(", ", removeResult.Errors.Select(e => e.Description)));
        }
        var addResult = await _userManager.AddPasswordAsync(user, IdentityHelpers.GenerateRandomPassword());
        if (!addResult.Succeeded)
            throw new InvalidOperationException(string.Join(", ", addResult.Errors.Select(e => e.Description)));

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException(string.Join(", ", updateResult.Errors.Select(e => e.Description)));

        // Apply the requested role if any.
        if (request.RoleId.HasValue)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId.Value.ToString());
            if (role != null
                && !await _userManager.IsInRoleAsync(user, role.Name!))
            {
                await _userManager.AddToRoleAsync(user, role.Name!);
            }
        }

        var emailSent = await SendWelcomeEmailAsync(request.Email);
        if (!emailSent)
        {
            _logger.LogError("Failed to send welcome password setup email to user {Email}", request.Email);
        }

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Created,
                EntityName = "User",
                EntityId = user.Id.ToString(),
                Details = "Restored from previously deleted account"
            });
        }

        var roles = await _userManager.GetRolesAsync(user);
        return await MapToUserResponseAsync(user, roles.ToList());
    }
}
