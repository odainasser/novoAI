using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Web.Services;

public interface ICurrentUserService
{
    Task<(Guid UserId, string UserName)> GetCurrentUserAsync();
}

public class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authStateProvider;

    public CurrentUserService(AuthenticationStateProvider authStateProvider)
    {
        _authStateProvider = authStateProvider;
    }

    public async Task<(Guid UserId, string UserName)> GetCurrentUserAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            return ExtractUser(user);
        }

        return (Guid.Empty, "System");
    }

    private (Guid UserId, string UserName) ExtractUser(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        var nameClaim = user.FindFirst(ClaimTypes.Name) ?? user.FindFirst(ClaimTypes.Email);
        
        if (Guid.TryParse(idClaim?.Value, out var userId))
        {
            return (userId, nameClaim?.Value ?? "Unknown");
        }
        
        return (Guid.Empty, "System");
    }
}
