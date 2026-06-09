using Application.Common.Models;
using Application.Features.Cashiers;
using Application.Features.UserLogs;
using Application.Services;
using Application.Common.Interfaces;
using Application.Common.Behaviors;
using Domain.Constants;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Infrastructure.Services;

public class CashierService : ICashierService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IUserLogService _userLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediaService _mediaService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;
    private readonly IAppConfiguration _configuration;
    private readonly ILogger<CashierService> _logger;

    public CashierService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IUserLogService userLogService,
        ICurrentUserService currentUserService,
        IMediaService mediaService,
        ApplicationDbContext dbContext,
        IIdentityService identityService,
        IEmailService emailService,
        IAppConfiguration configuration,
        ILogger<CashierService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _userLogService = userLogService;
        _currentUserService = currentUserService;
        _mediaService = mediaService;
        _dbContext = dbContext;
        _identityService = identityService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaginatedList<CashierResponse>> GetAllCashiersAsync(int pageNumber, int pageSize, string? search = null, bool? isActive = null, Guid? warehouseId = null, CancellationToken cancellationToken = default, IReadOnlyList<Guid>? warehouseIds = null)
    {
        // Get all users in Cashier role
        var cashierUsers = await _userManager.GetUsersInRoleAsync(Roles.Cashier);
        var cashierIds = cashierUsers.Select(u => u.Id).ToList();

        var query = _userManager.Users
            .Where(u => cashierIds.Contains(u.Id));

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

        // Apply store/warehouse filter (matches cashiers assigned to that store).
        // A single warehouseId wins; otherwise an optional warehouseIds set scopes
        // to cashiers assigned to any of those warehouses (branch-locked queries).
        if (warehouseId.HasValue)
        {
            var assignedCashierIds = await _dbContext.CashierWarehouses
                .Where(cw => cw.WarehouseId == warehouseId.Value)
                .Select(cw => cw.CashierId)
                .ToListAsync(cancellationToken);
            query = query.Where(u => assignedCashierIds.Contains(u.Id));
        }
        else if (warehouseIds is { Count: > 0 })
        {
            var assignedCashierIds = await _dbContext.CashierWarehouses
                .Where(cw => warehouseIds.Contains(cw.WarehouseId))
                .Select(cw => cw.CashierId)
                .Distinct()
                .ToListAsync(cancellationToken);
            query = query.Where(u => assignedCashierIds.Contains(u.Id));
        }

        var count = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(u => u.UpdatedAt ?? u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var responses = new List<CashierResponse>();
        foreach (var user in users)
        {
            responses.Add(await MapToCashierResponseAsync(user));
        }

        return new PaginatedList<CashierResponse>(responses, count, pageNumber, pageSize);
    }

    public async Task<CashierResponse?> GetCashierByIdAsync(Guid cashierId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(cashierId.ToString());
        if (user == null) return null;

        // Verify user is in Cashier role
        var isInCashierRole = await _userManager.IsInRoleAsync(user, Roles.Cashier);
        if (!isInCashierRole) return null;

        return await MapToCashierResponseAsync(user);
    }

    public async Task<CashierResponse> CreateCashierAsync(CreateCashierRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.WarehouseIds.Any())
        {
            throw new ArgumentException("At least one store must be assigned to the cashier.");
        }

        // Look up by normalized email including soft-deleted rows. UserManager.FindByEmailAsync
        // honors the IsDeleted query filter on ApplicationUser, so it would miss a soft-deleted
        // record and the subsequent INSERT would violate Identity's unique NormalizedEmail /
        // NormalizedUserName index, surfacing as a 500.
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
            return await ReactivateCashierAsync(existingUser, request, cancellationToken);
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
            CanRefund = request.CanRefund,
            WarehouseId = request.WarehouseIds.First(),
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, randomPassword);

        if (!result.Succeeded)
        {
            var failures = result.Errors.Select(e => IdentityHelpers.MapIdentityErrorToValidationFailure(e));
            throw new ValidationException(failures);
        }

        // Automatically assign Cashier role
        var cashierRole = await _roleManager.FindByNameAsync(Roles.Cashier);
        if (cashierRole != null)
        {
            await _userManager.AddToRoleAsync(user, Roles.Cashier);
        }

        // Save all assigned warehouses to junction table
        foreach (var warehouseId in request.WarehouseIds)
        {
            _dbContext.CashierWarehouses.Add(new Domain.Entities.CashierWarehouse
            {
                CashierId = user.Id,
                WarehouseId = warehouseId
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Send welcome email so the cashier can set their own password
        var emailSent = await SendWelcomeEmailAsync(request.Email);
        if (!emailSent)
        {
            _logger.LogError("Failed to send welcome password setup email to cashier {Email}", request.Email);
        }

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Created,
                EntityName = "Cashier",
                EntityId = user.Id.ToString(),
                Details = null
            });
        }

        return await MapToCashierResponseAsync(user);
    }

    public async Task<CashierResponse> UpdateCashierAsync(Guid cashierId, UpdateCashierRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(cashierId.ToString());
        if (user == null)
        {
            throw new UserNotFoundException($"Cashier with ID '{cashierId}' not found.");
        }

        // Verify user is in Cashier role
        var isInCashierRole = await _userManager.IsInRoleAsync(user, Roles.Cashier);
        if (!isInCashierRole)
        {
            throw new UserNotFoundException($"Cashier with ID '{cashierId}' not found.");
        }

        if (!request.WarehouseIds.Any())
        {
            throw new ArgumentException("At least one store must be assigned to the cashier.");
        }

        // Determine active warehouse: keep existing if still in list, otherwise use first
        var newActiveWarehouseId = user.WarehouseId.HasValue && request.WarehouseIds.Contains(user.WarehouseId.Value)
            ? user.WarehouseId.Value
            : request.WarehouseIds.First();

        // System users: allow updating store assignment, active status, can refund, and phone number
        if (user.IsSystemUser)
        {
            user.WarehouseId = newActiveWarehouseId;
            user.IsActive = request.IsActive;
            user.CanRefund = request.CanRefund;
            user.PhoneNumber = request.PhoneNumber;
            user.UpdatedAt = DateTime.UtcNow;

            var sysResult = await _userManager.UpdateAsync(user);
            if (!sysResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(", ", sysResult.Errors.Select(e => e.Description)));
            }

            await SyncCashierWarehousesAsync(cashierId, request.WarehouseIds, cancellationToken);

            var (sysUserId, sysUserName) = await _currentUserService.GetCurrentUserAsync();
            if (sysUserId != Guid.Empty)
            {
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = sysUserId,
                    UserName = sysUserName,
                    Action = AuditAction.Updated,
                    EntityName = "Cashier",
                    EntityId = user.Id.ToString(),
                    Details = "Store assignment updated for system cashier"
                });
            }

            return await MapToCashierResponseAsync(user);
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
        user.CanRefund = request.CanRefund;
        user.WarehouseId = newActiveWarehouseId;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        await SyncCashierWarehousesAsync(cashierId, request.WarehouseIds, cancellationToken);

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Updated,
                EntityName = "Cashier",
                EntityId = user.Id.ToString(),
                Details = null
            });
        }

        return await MapToCashierResponseAsync(user);
    }

    public async Task DeleteCashierAsync(Guid cashierId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(cashierId.ToString());
        if (user == null)
        {
            throw new UserNotFoundException($"Cashier with ID '{cashierId}' not found.");
        }

        // Verify user is in Cashier role
        var isInCashierRole = await _userManager.IsInRoleAsync(user, Roles.Cashier);
        if (!isInCashierRole)
        {
            throw new UserNotFoundException($"Cashier with ID '{cashierId}' not found.");
        }

        // Prevent deleting system users
        if (user.IsSystemUser)
        {
            throw new SystemUserModificationException();
        }

        var cashierIdValue = user.Id;

        if (await _dbContext.Shifts.AnyAsync(s => s.CashierId == cashierIdValue && s.Status == ShiftStatus.Active, cancellationToken))
            throw new InvalidOperationException("Cannot delete cashier: there is an active shift. End the shift first.");

        if (await _dbContext.Orders.AnyAsync(o => o.CashierId == cashierIdValue, cancellationToken))
            throw new InvalidOperationException("Cannot delete cashier: linked to existing orders.");

        if (await _dbContext.Shifts.AnyAsync(s => s.CashierId == cashierIdValue, cancellationToken))
            throw new InvalidOperationException("Cannot delete cashier: linked to existing shifts.");

        // Remove cashier-warehouse assignments (junction with no soft delete)
        var cashierWarehouses = await _dbContext.CashierWarehouses
            .Where(cw => cw.CashierId == cashierIdValue)
            .ToListAsync(cancellationToken);
        if (cashierWarehouses.Count > 0)
            _dbContext.CashierWarehouses.RemoveRange(cashierWarehouses);

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
                EntityName = "Cashier",
                EntityId = user.Id.ToString(),
                Details = null
            });
        }
    }

    public async Task<IEnumerable<CashierResponse>> GetActiveCashiersAsync(CancellationToken cancellationToken = default)
    {
        var cashierUsers = await _userManager.GetUsersInRoleAsync(Roles.Cashier);
        var activeCashiers = cashierUsers.Where(u => u.IsActive).ToList();

        var responses = new List<CashierResponse>();
        foreach (var user in activeCashiers)
        {
            responses.Add(await MapToCashierResponseAsync(user));
        }

        return responses;
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return false;

        return await _userManager.IsInRoleAsync(user, Roles.Cashier);
    }

    public async Task<CashierResponse> SwitchStoreAsync(Guid cashierId, Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(cashierId.ToString());
        if (user == null)
        {
            throw new UserNotFoundException($"Cashier with ID '{cashierId}' not found.");
        }

        // Verify the warehouse is in the cashier's assigned stores
        var isAssigned = await _dbContext.CashierWarehouses
            .AnyAsync(cw => cw.CashierId == cashierId && cw.WarehouseId == warehouseId, cancellationToken);

        if (!isAssigned)
        {
            throw new InvalidOperationException("The selected store is not assigned to this cashier.");
        }

        // Verify no active shift
        var hasActiveShift = await _dbContext.Shifts
            .AnyAsync(s => s.CashierId == cashierId && s.Status == Domain.Enums.ShiftStatus.Active, cancellationToken);

        if (hasActiveShift)
        {
            throw new InvalidOperationException("You must end your current shift before switching stores.");
        }

        user.WarehouseId = warehouseId;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Also sync the domain User entity so ShiftService reads the correct warehouse
        var domainUser = await _dbContext.DomainUsers.FirstOrDefaultAsync(u => u.Id == cashierId, cancellationToken);
        if (domainUser != null)
        {
            domainUser.WarehouseId = warehouseId;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await MapToCashierResponseAsync(user);
    }

    public async Task<IEnumerable<AssignedWarehouseDto>> GetAssignedStoresAsync(Guid cashierId, CancellationToken cancellationToken = default)
    {
        var warehouseIds = await _dbContext.CashierWarehouses
            .Where(cw => cw.CashierId == cashierId)
            .Select(cw => cw.WarehouseId)
            .ToListAsync(cancellationToken);

        if (!warehouseIds.Any())
            return Enumerable.Empty<AssignedWarehouseDto>();

        var warehouses = await _dbContext.Warehouses
            .AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .ToListAsync(cancellationToken);

        return warehouses.Select(w => new AssignedWarehouseDto
        {
            Id = w.Id,
            NameEn = w.NameEn,
            NameAr = w.NameAr
        });
    }

    private async Task SyncCashierWarehousesAsync(Guid cashierId, List<Guid> newWarehouseIds, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CashierWarehouses
            .Where(cw => cw.CashierId == cashierId)
            .ToListAsync(cancellationToken);

        var existingIds = existing.Select(cw => cw.WarehouseId).ToHashSet();
        var newIds = newWarehouseIds.ToHashSet();

        // Remove warehouses no longer in the list
        var toRemove = existing.Where(cw => !newIds.Contains(cw.WarehouseId)).ToList();
        _dbContext.CashierWarehouses.RemoveRange(toRemove);

        // Add new warehouses
        foreach (var warehouseId in newIds.Where(id => !existingIds.Contains(id)))
        {
            _dbContext.CashierWarehouses.Add(new Domain.Entities.CashierWarehouse
            {
                CashierId = cashierId,
                WarehouseId = warehouseId
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CashierResponse> MapToCashierResponseAsync(ApplicationUser user)
    {
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

        // Look up active warehouse/store info
        string? warehouseNameEn = null;
        string? warehouseNameAr = null;
        if (user.WarehouseId.HasValue)
        {
            var warehouse = await _dbContext.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == user.WarehouseId.Value);
            if (warehouse != null)
            {
                warehouseNameEn = warehouse.NameEn;
                warehouseNameAr = warehouse.NameAr;
            }
        }

        // Load all assigned warehouses
        var assignedWarehouseIds = await _dbContext.CashierWarehouses
            .Where(cw => cw.CashierId == user.Id)
            .Select(cw => cw.WarehouseId)
            .ToListAsync();

        List<AssignedWarehouseDto> assignedWarehouses = new();
        if (assignedWarehouseIds.Any())
        {
            var warehouses = await _dbContext.Warehouses
                .AsNoTracking()
                .Where(w => assignedWarehouseIds.Contains(w.Id))
                .ToListAsync();

            assignedWarehouses = warehouses.Select(w => new AssignedWarehouseDto
            {
                Id = w.Id,
                NameEn = w.NameEn,
                NameAr = w.NameAr
            }).ToList();
        }

        return new CashierResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
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
            IsSystemUser = user.IsSystemUser,
            AvatarUrl = avatarUrl,
            WarehouseId = user.WarehouseId,
            WarehouseNameEn = warehouseNameEn,
            WarehouseNameAr = warehouseNameAr,
            CanRefund = user.CanRefund,
            AssignedWarehouses = assignedWarehouses
        };
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

    private async Task<CashierResponse> ReactivateCashierAsync(
        ApplicationUser user,
        CreateCashierRequest request,
        CancellationToken cancellationToken)
    {
        // Restore the soft-deleted account in place so historical references
        // (logs, audit) stay intact and the unique email/username index is respected.
        user.IsDeleted = false;
        user.DeletedAt = null;
        user.DeletedBy = null;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.IsActive = request.IsActive;
        user.CanRefund = request.CanRefund;
        user.WarehouseId = request.WarehouseIds.First();
        user.UpdatedAt = DateTime.UtcNow;
        user.EmailConfirmed = false;
        user.UserName = request.Email;
        user.Email = request.Email;

        // Force a new password so the previous owner's old credentials can't be reused.
        // The welcome email below lets them set their own.
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

        // Ensure Cashier role.
        if (!await _userManager.IsInRoleAsync(user, Roles.Cashier))
        {
            await _userManager.AddToRoleAsync(user, Roles.Cashier);
        }

        // Replace warehouse assignments with the requested set. DeleteCashierAsync hard-deletes
        // these rows, but be defensive in case of partial state.
        var oldAssignments = await _dbContext.CashierWarehouses
            .Where(cw => cw.CashierId == user.Id)
            .ToListAsync(cancellationToken);
        if (oldAssignments.Count > 0)
            _dbContext.CashierWarehouses.RemoveRange(oldAssignments);
        foreach (var warehouseId in request.WarehouseIds)
        {
            _dbContext.CashierWarehouses.Add(new Domain.Entities.CashierWarehouse
            {
                CashierId = user.Id,
                WarehouseId = warehouseId
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        var emailSent = await SendWelcomeEmailAsync(request.Email);
        if (!emailSent)
        {
            _logger.LogError("Failed to send welcome password setup email to cashier {Email}", request.Email);
        }

        var (currentUserId, currentUserName) = await _currentUserService.GetCurrentUserAsync();
        if (currentUserId != Guid.Empty)
        {
            await _userLogService.LogAsync(new CreateUserLogRequest
            {
                UserId = currentUserId,
                UserName = currentUserName,
                Action = AuditAction.Created,
                EntityName = "Cashier",
                EntityId = user.Id.ToString(),
                Details = "Restored from previously deleted account"
            });
        }

        return await MapToCashierResponseAsync(user);
    }
}
