using Web.Models.Auth;

namespace Web.Services;

public interface IAuthenticationService
{
    // Authentication
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<AuthResponse> LogoutWithValidationAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<UserDto?> GetCurrentUserAsync();
    
    // Password Management
    Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request);
    Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request);
    
    // Profile Management
    Task<AuthResponse> UpdateProfileAsync(object profileData);
}
