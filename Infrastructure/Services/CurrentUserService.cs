using Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of ICurrentUserService that only relies on IHttpContextAccessor.
/// This avoids introducing a circular dependency between Web.Authentication and infrastructure services.
/// For Blazor Server calls (SignalR) HttpContext may be unavailable; in that case this returns (Guid.Empty, "System").
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<(Guid UserId, string UserName)> GetCurrentUserAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var nameClaim = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
            if (Guid.TryParse(idClaim, out var userId))
            {
                return Task.FromResult((userId, nameClaim ?? "Unknown"));
            }
        }

        return Task.FromResult((Guid.Empty, "System"));
    }
}
