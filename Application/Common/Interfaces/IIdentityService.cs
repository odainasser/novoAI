using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IIdentityService
{
    Task<(bool Success, string[] Errors, User? User)> CreateUserAsync(User user, string password);
    Task<(bool Success, string Message, User? User)> ValidateCredentialsAsync(string email, string password);
    Task<User?> FindByEmailAsync(string email);
    Task<User?> FindByIdAsync(Guid userId);
    Task<IList<string>> GetUserRolesAsync(Guid userId);
    Task<IList<string>> GetUserPermissionsAsync(Guid userId);
    Task<bool> CheckPasswordAsync(User user, string password);
    
    Task<string> GenerateEmailConfirmationTokenAsync(Guid userId);
    Task<(bool Success, string[] Errors)> ConfirmEmailAsync(Guid userId, string token);
    Task<bool> IsEmailConfirmedAsync(Guid userId);
    
    Task<string> GeneratePasswordResetTokenAsync(string email);
    Task<(bool Success, string[] Errors)> ResetPasswordAsync(string email, string token, string newPassword);
    Task<(bool Success, string[] Errors)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    
    Task<(bool Success, string Message)> AccessFailedAsync(Guid userId);
    Task ResetAccessFailedCountAsync(Guid userId);
    Task<bool> IsLockedOutAsync(Guid userId);
    Task<(bool Success, string[] Errors)> SetLockoutEndDateAsync(Guid userId, DateTimeOffset? lockoutEnd);
    Task<(bool Success, string[] Errors)> SetLockoutEnabledAsync(Guid userId, bool enabled);
    
    Task<(bool Success, string[] Errors)> UpdateUserAsync(User user);
    Task<(bool Success, string[] Errors)> DeleteUserAsync(Guid userId);
    Task<(bool Success, string[] Errors)> AddToRoleAsync(Guid userId, string roleName);
    Task<(bool Success, string[] Errors)> RemoveFromRoleAsync(Guid userId, string roleName);
    Task<bool> IsInRoleAsync(Guid userId, string roleName);
    
    Task<string> GenerateChangePhoneNumberTokenAsync(Guid userId, string phoneNumber);
    Task<(bool Success, string[] Errors)> ChangePhoneNumberAsync(Guid userId, string phoneNumber, string token);
    Task<(bool Success, string[] Errors)> SetPhoneNumberAsync(Guid userId, string phoneNumber);
    
    Task UpdateSecurityStampAsync(Guid userId);
    Task<string> GenerateUserTokenAsync(Guid userId, string purpose);
    Task<bool> VerifyUserTokenAsync(Guid userId, string purpose, string token);
}
