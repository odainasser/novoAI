using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Web.Services;

namespace Web.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IAuthenticationService _authenticationService;
    private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public CustomAuthenticationStateProvider(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // Check if user is authenticated first
            var isAuthenticated = await _authenticationService.IsAuthenticatedAsync();
            if (!isAuthenticated)
            {
                return new AuthenticationState(_anonymous);
            }

            // Get user data
            var user = await _authenticationService.GetCurrentUserAsync();
            
            if (user == null)
            {
                // Self-healing: If authenticated but no user data, force logout to clean up state
                await _authenticationService.LogoutAsync();
                return new AuthenticationState(_anonymous);
            }

            var display = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : (!string.IsNullOrEmpty(user.FirstName) || !string.IsNullOrEmpty(user.LastName) ? $"{user.FirstName} {user.LastName}".Trim() : user.Email);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, display ?? "")
            };

            if (!string.IsNullOrEmpty(user.FirstName))
                claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
            
            if (!string.IsNullOrEmpty(user.LastName))
                claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

            // Add roles as claims
            if (user.Roles != null)
            {
                foreach (var role in user.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }

            // Add permissions as claims
            if (user.Permissions != null)
            {
                foreach (var permission in user.Permissions)
                {
                    claims.Add(new Claim("permission", permission));
                }
            }

            var identity = new ClaimsIdentity(claims, "customAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            return new AuthenticationState(claimsPrincipal);
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
