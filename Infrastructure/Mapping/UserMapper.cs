using Domain.Entities;
using Infrastructure.Identity;

namespace Infrastructure.Mapping;

public static class UserMapper
{
    public static User ToDomainUser(ApplicationUser identityUser)
    {
        return new User
        {
            Id = identityUser.Id,
            Email = identityUser.Email ?? string.Empty,
            FirstName = identityUser.FirstName,
            LastName = identityUser.LastName,
            IsActive = identityUser.IsActive,
            EmailConfirmed = identityUser.EmailConfirmed,
            PhoneNumber = identityUser.PhoneNumber,
            PhoneNumberConfirmed = identityUser.PhoneNumberConfirmed,
            TwoFactorEnabled = identityUser.TwoFactorEnabled,
            LockoutEnd = identityUser.LockoutEnd?.UtcDateTime,
            LockoutEnabled = identityUser.LockoutEnabled,
            AccessFailedCount = identityUser.AccessFailedCount,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static ApplicationUser ToIdentityUser(User domainUser)
    {
        return new ApplicationUser
        {
            Id = domainUser.Id,
            UserName = domainUser.Email,
            Email = domainUser.Email,
            FirstName = domainUser.FirstName,
            LastName = domainUser.LastName,
            IsActive = domainUser.IsActive,
            EmailConfirmed = domainUser.EmailConfirmed,
            PhoneNumber = domainUser.PhoneNumber,
            PhoneNumberConfirmed = domainUser.PhoneNumberConfirmed,
            TwoFactorEnabled = domainUser.TwoFactorEnabled,
            LockoutEnd = domainUser.LockoutEnd.HasValue 
                ? new DateTimeOffset(domainUser.LockoutEnd.Value) 
                : null,
            LockoutEnabled = domainUser.LockoutEnabled,
            AccessFailedCount = domainUser.AccessFailedCount
        };
    }

    public static void UpdateIdentityUser(ApplicationUser identityUser, User domainUser)
    {
        identityUser.Email = domainUser.Email;
        identityUser.UserName = domainUser.Email;
        identityUser.FirstName = domainUser.FirstName;
        identityUser.LastName = domainUser.LastName;
        identityUser.IsActive = domainUser.IsActive;
        identityUser.EmailConfirmed = domainUser.EmailConfirmed;
        identityUser.PhoneNumber = domainUser.PhoneNumber;
        identityUser.PhoneNumberConfirmed = domainUser.PhoneNumberConfirmed;
        identityUser.TwoFactorEnabled = domainUser.TwoFactorEnabled;
        identityUser.LockoutEnd = domainUser.LockoutEnd.HasValue 
            ? new DateTimeOffset(domainUser.LockoutEnd.Value) 
            : null;
        identityUser.LockoutEnabled = domainUser.LockoutEnabled;
        identityUser.AccessFailedCount = domainUser.AccessFailedCount;
    }
}
