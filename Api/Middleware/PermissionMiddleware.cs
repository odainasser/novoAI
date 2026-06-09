using System.Security.Claims;
using Application.Common.Interfaces;

namespace Api.Middleware;

public class PermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PermissionMiddleware> _logger;

    public PermissionMiddleware(RequestDelegate next, ILogger<PermissionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdentityService identityService)
    {
        var user = context.User;
        
        if (user.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                IList<string> userPermissions = new List<string>();

                try
                {
                    userPermissions = await identityService.GetUserPermissionsAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load permissions for user {UserId}", userId);
                    userPermissions = new List<string>();
                }
                
                foreach (var permission in userPermissions)
                {
                    // Ensure we don't add duplicate claims (ClaimsIdentity.AddClaim will allow duplicates otherwise)
                    if (!user.HasClaim(c => c.Type == "permission" && c.Value == permission))
                    {
                        ((ClaimsIdentity)user.Identity!).AddClaim(new Claim("permission", permission));
                    }
                }
                
                _logger.LogInformation("User {UserId} has {PermissionCount} permissions loaded", userId, userPermissions.Count);
            }
        }
        
        await _next(context);
    }
}
