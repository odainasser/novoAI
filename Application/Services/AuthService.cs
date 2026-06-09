using Application.Common.Interfaces;
using Application.Features.Auth;
using Application.Features.UserLogs;
using Domain.Entities;
using Domain.Constants;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IAppConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IUserLogService _userLogService;
    private readonly IWarehouseService _warehouseService;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public AuthService(
        IIdentityService identityService,
        ITokenService tokenService,
        IEmailService emailService,
        IAppConfiguration configuration,
        ILogger<AuthService> logger,
        IUserLogService userLogService,
        IWarehouseService warehouseService,
        IRefreshTokenStore refreshTokenStore)
    {
        _identityService = identityService;
        _tokenService = tokenService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
        _userLogService = userLogService;
        _warehouseService = warehouseService;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _identityService.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "User with this email already exists"
            };
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow
        };

        var (success, errors, createdUser) = await _identityService.CreateUserAsync(user, request.Password);

        if (!success)
        {
            return new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", errors)
            };
        }

        // Assign Cashier role by default for public registration because Client is removed
        await _identityService.AddToRoleAsync(createdUser!.Id, Roles.Cashier);

        var roles = await _identityService.GetUserRolesAsync(createdUser!.Id);
        var permissions = await _identityService.GetUserPermissionsAsync(createdUser.Id);
        var createdUserDisplay = GetDisplayName(createdUser);
        var token = _tokenService.GenerateJwtToken(createdUser.Id, createdUser.Email, createdUserDisplay, roles, permissions);
        var refreshToken = await IssueRefreshTokenAsync(createdUser.Id, null);

        await _userLogService.LogAsync(new CreateUserLogRequest
        {
            UserId = createdUser.Id,
            UserName = createdUserDisplay,
            Action = AuditAction.Created,
            EntityName = "User",
            EntityId = createdUser.Id.ToString(),
            Details = null
        });

        return new AuthResponse
        {
            Success = true,
            Message = "Registration successful",
            Token = token,
            RefreshToken = refreshToken,
            AccessTokenExpiresInSeconds = _tokenService.AccessTokenLifetimeSeconds,
            User = new UserDto
            {
                Id = createdUser!.Id,
                Email = createdUser.Email,
                FirstName = createdUser.FirstName,
                LastName = createdUser.LastName,
                DisplayName = createdUserDisplay,
                IsActive = createdUser.IsActive,
                WarehouseId = createdUser.WarehouseId,
                CanRefund = createdUser.CanRefund,
                Roles = roles.ToList(),
                Permissions = permissions.ToList()
            }
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? clientIp = null)
    {
        _logger.LogInformation("Login attempt for email: {Email}", request.Email);

        var (success, message, user) = await _identityService.ValidateCredentialsAsync(request.Email, request.Password);

        if (!success || user == null)
        {
            _logger.LogWarning("Login failed for email: {Email}. Reason: {Message}", request.Email, message);
            return new AuthResponse
            {
                Success = false,
                Message = message
            };
        }

        _logger.LogInformation("Login successful for email: {Email}, UserId: {UserId}", request.Email, user.Id);

        var roles = await _identityService.GetUserRolesAsync(user.Id);
        var permissions = await _identityService.GetUserPermissionsAsync(user.Id);

        // If user is a Cashier, check if their assigned warehouse (store) is active
        if (roles.Contains(Roles.Cashier) && user.WarehouseId.HasValue)
        {
            var warehouse = await _warehouseService.GetWarehouseByIdAsync(user.WarehouseId.Value);
            if (warehouse == null || !warehouse.IsActive)
            {
                _logger.LogWarning("Cashier login blocked - warehouse {WarehouseId} is deactivated for user {UserId}", user.WarehouseId, user.Id);
                return new AuthResponse
                {
                    Success = false,
                    Message = "StoreDeactivatedLoginError"
                };
            }
        }

        var userDisplay = GetDisplayName(user);
        var token = _tokenService.GenerateJwtToken(user.Id, user.Email, userDisplay, roles, permissions);
        var refreshToken = await IssueRefreshTokenAsync(user.Id, clientIp);

        // Login logging removed per user request - login events are not recorded in user logs

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Token = token,
            RefreshToken = refreshToken,
            AccessTokenExpiresInSeconds = _tokenService.AccessTokenLifetimeSeconds,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = userDisplay,
                IsActive = user.IsActive,
                WarehouseId = user.WarehouseId,
                CanRefund = user.CanRefund,
                Roles = roles.ToList(),
                Permissions = permissions.ToList()
            }
        };
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, string? clientIp = null)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return new AuthResponse { Success = false, Message = "InvalidRefreshToken" };
        }

        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokenStore.FindByHashAsync(hash);

        if (stored is null || !stored.IsActive)
        {
            // Possible reuse of a revoked token: revoke entire chain for safety.
            if (stored is { RevokedAt: not null })
            {
                _logger.LogWarning("Refresh token reuse detected for user {UserId}. Revoking all tokens.", stored.UserId);
                await _refreshTokenStore.RevokeAllForUserAsync(stored.UserId, clientIp);
                await _refreshTokenStore.SaveChangesAsync();
            }
            return new AuthResponse { Success = false, Message = "InvalidRefreshToken" };
        }

        var user = await _identityService.FindByIdAsync(stored.UserId);
        if (user is null || !user.IsActive)
        {
            stored.RevokedAt = DateTime.UtcNow;
            stored.RevokedByIp = clientIp;
            await _refreshTokenStore.UpdateAsync(stored);
            await _refreshTokenStore.SaveChangesAsync();
            return new AuthResponse { Success = false, Message = "InvalidRefreshToken" };
        }

        var roles = await _identityService.GetUserRolesAsync(user.Id);
        var permissions = await _identityService.GetUserPermissionsAsync(user.Id);
        var display = GetDisplayName(user);

        // Rotate: revoke current, issue new
        var newRefresh = _tokenService.GenerateRefreshToken();
        var newHash = _tokenService.HashRefreshToken(newRefresh);
        var now = DateTime.UtcNow;

        stored.RevokedAt = now;
        stored.RevokedByIp = clientIp;
        stored.ReplacedByTokenHash = newHash;
        await _refreshTokenStore.UpdateAsync(stored);

        await _refreshTokenStore.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAt = now.AddDays(_tokenService.RefreshTokenLifetimeDays),
            CreatedAt = now,
            CreatedByIp = clientIp
        });

        await _refreshTokenStore.SaveChangesAsync();

        var accessToken = _tokenService.GenerateJwtToken(user.Id, user.Email, display, roles, permissions);

        return new AuthResponse
        {
            Success = true,
            Message = "Refreshed",
            Token = accessToken,
            RefreshToken = newRefresh,
            AccessTokenExpiresInSeconds = _tokenService.AccessTokenLifetimeSeconds,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = display,
                IsActive = user.IsActive,
                WarehouseId = user.WarehouseId,
                CanRefund = user.CanRefund,
                Roles = roles.ToList(),
                Permissions = permissions.ToList()
            }
        };
    }

    public async Task<AuthResponse> RevokeRefreshTokenAsync(string refreshToken, string? clientIp = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return new AuthResponse { Success = true, Message = "Revoked" };
        }

        var hash = _tokenService.HashRefreshToken(refreshToken);
        var stored = await _refreshTokenStore.FindByHashAsync(hash);
        if (stored is { IsActive: true })
        {
            stored.RevokedAt = DateTime.UtcNow;
            stored.RevokedByIp = clientIp;
            await _refreshTokenStore.UpdateAsync(stored);
            await _refreshTokenStore.SaveChangesAsync();
        }

        return new AuthResponse { Success = true, Message = "Revoked" };
    }

    private async Task<string> IssueRefreshTokenAsync(Guid userId, string? clientIp)
    {
        var raw = _tokenService.GenerateRefreshToken();
        var hash = _tokenService.HashRefreshToken(raw);
        var now = DateTime.UtcNow;

        await _refreshTokenStore.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = now.AddDays(_tokenService.RefreshTokenLifetimeDays),
            CreatedAt = now,
            CreatedByIp = clientIp
        });
        await _refreshTokenStore.SaveChangesAsync();

        return raw;
    }

    #region Email Confirmation

    public async Task<AuthResponse> SendEmailConfirmationAsync(string email)
    {
        var user = await _identityService.FindByEmailAsync(email);
        if (user == null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "User not found"
            };
        }

        var token = await _identityService.GenerateEmailConfirmationTokenAsync(user.Id);
        var appUrl = _configuration.GetAppUrl();
        var confirmationLink = $"{appUrl}/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        
        var emailSent = await _emailService.SendEmailConfirmationAsync(user.Email, confirmationLink);

        if (!emailSent)
        {
            _logger.LogError("Failed to send email confirmation to {Email}", email);
            return new AuthResponse
            {
                Success = false,
                Message = "Failed to send confirmation email. Please try again later."
            };
        }

        return new AuthResponse
        {
            Success = true,
            Message = "Confirmation email sent successfully"
        };
    }

    public async Task<AuthResponse> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Invalid user ID"
            };
        }

        var (success, errors) = await _identityService.ConfirmEmailAsync(userId, request.Token);

        if (success)
        {
            var user = await _identityService.FindByIdAsync(userId);
            if (user != null)
            {
                var userDisplay = GetDisplayName(user);
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = user.Id,
                    UserName = userDisplay,
                    Action = AuditAction.EmailVerified,
                    EntityName = "User",
                    EntityId = user.Id.ToString(),
                    Details = null
                });
            }
        }

        return new AuthResponse
        {
            Success = success,
            Message = success ? "Email confirmed successfully" : string.Join(", ", errors)
        };
    }

    #endregion

    #region Password Management

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _identityService.FindByEmailAsync(request.Email);
        
        // Don't reveal if user exists or not (security best practice)
        if (user == null)
        {
            return new AuthResponse
            {
                Success = true,
                Message = "If an account with that email exists, a password reset link has been sent"
            };
        }

        var token = await _identityService.GeneratePasswordResetTokenAsync(request.Email);
        var appUrl = _configuration.GetAppUrl();
        
        // Convert token to Base64Url to avoid + / = characters being corrupted in URL query strings
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        var base64UrlToken = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        
        var resetLink = $"{appUrl}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={base64UrlToken}";
        
        var emailSent = await _emailService.SendPasswordResetAsync(user.Email, resetLink);

        if (!emailSent)
        {
            _logger.LogError("Failed to send password reset email to {Email}", request.Email);
        }

        return new AuthResponse
        {
            Success = true,
            Message = "If an account with that email exists, a password reset link has been sent"
        };
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        // Validate confirm password and password rules
        if (request.NewPassword != request.ConfirmPassword)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Passwords do not match."
            };
        }

        var pwdValid = ValidatePassword(request.NewPassword, out var pwdMessage);
        if (!pwdValid)
        {
            return new AuthResponse
            {
                Success = false,
                Message = pwdMessage
            };
        }

        var decodedToken = DecodeBase64UrlToken(request.Token);
        _logger.LogInformation("Reset password attempt for {Email}. Token length: {TokenLen}, Decoded length: {DecodedLen}",
            request.Email, request.Token.Length, decodedToken.Length);

        var (success, errors) = await _identityService.ResetPasswordAsync(
            request.Email,
            decodedToken,
            request.NewPassword);

        if (!success)
        {
            _logger.LogWarning("Reset password failed for {Email}. Errors: {Errors}", request.Email, string.Join(", ", errors));
        }

        if (success)
        {
            var user = await _identityService.FindByEmailAsync(request.Email);
            if (user != null)
            {
                var userDisplay = GetDisplayName(user);
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = user.Id,
                    UserName = userDisplay,
                    Action = AuditAction.PasswordReset,
                    EntityName = "User",
                    EntityId = user.Id.ToString(),
                    Details = null
                });
            }
        }

        return new AuthResponse
        {
            Success = success,
            Message = success ? "Password reset successfully" : string.Join(", ", errors)
        };
    }

    public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        // Validate confirm password and password rules
        if (request.NewPassword != request.ConfirmPassword)
        {
            return new AuthResponse
            {
                Success = false,
                Message = "Passwords do not match."
            };
        }

        var pwdValid = ValidatePassword(request.NewPassword, out var pwdMessage);
        if (!pwdValid)
        {
            return new AuthResponse
            {
                Success = false,
                Message = pwdMessage
            };
        }

        var (success, errors) = await _identityService.ChangePasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword);

        if (success)
        {
            var user = await _identityService.FindByIdAsync(userId);
            if (user != null)
            {
                var userDisplay = GetDisplayName(user);
                await _userLogService.LogAsync(new CreateUserLogRequest
                {
                    UserId = user.Id,
                    UserName = userDisplay,
                    Action = AuditAction.PasswordChanged,
                    EntityName = "User",
                    EntityId = user.Id.ToString(),
                    Details = null
                });
            }
        }

        return new AuthResponse
        {
            Success = success,
            Message = success ? "Password changed successfully" : string.Join(", ", errors)
        };
    }

    private bool ValidatePassword(string password, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(password))
        {
            message = "Password is required.";
            return false;
        }

        if (password.Length < 8)
        {
            message = "Password must be at least 8 characters long.";
            return false;
        }

        if (password.Length > 128)
        {
            message = "Password must not exceed 128 characters.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[A-Z]"))
        {
            message = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[a-z]"))
        {
            message = "Password must contain at least one lowercase letter.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[0-9]"))
        {
            message = "Password must contain at least one digit.";
            return false;
        }

        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
        {
            message = "Password must contain at least one special character.";
            return false;
        }

        return true;
    }

    private static string GetDisplayName(Domain.Entities.User user)
    {
        var first = user.FirstName?.Trim();
        var last = user.LastName?.Trim();
        var full = string.IsNullOrEmpty(first) && string.IsNullOrEmpty(last) ? null : $"{first} {last}".Trim();
        return string.IsNullOrEmpty(full) ? user.Email ?? "Unknown" : full;
    }

    private static string DecodeBase64UrlToken(string base64UrlToken)
    {
        try
        {
            // Convert Base64Url back to standard Base64
            var base64 = base64UrlToken
                .Replace('-', '+')
                .Replace('_', '/');
            
            // Add padding if needed
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            
            var tokenBytes = Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(tokenBytes);
        }
        catch
        {
            // If decoding fails, the token may be in the original (non-Base64Url) format
            return base64UrlToken;
        }
    }

    #endregion
}
