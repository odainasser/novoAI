using System.Collections.Concurrent;
using System.Text;
using Infrastructure.Configuration;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

/// <summary>
/// Dynamic token trust for the Apps integration module. ByteAI's own users (and
/// apps that share its symmetric JWT secret, like ByteMart) validate as before;
/// additionally, a bearer token whose issuer matches a registered ACTIVE app's
/// <c>JwtAuthority</c> is accepted, with signing keys discovered from that
/// authority's OIDC metadata (cached). This lets each registered app's users call
/// /api/assistant/ask with their own token — and the same token is then forwarded
/// back to the app's tool-provider endpoints, where the app validates it natively.
/// </summary>
public sealed class AppTokenTrust
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AppTokenTrust> _logger;

    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _oidcConfigs = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyDictionary<string, string>? _authorities;   // issuer -> app code
    private DateTime _authoritiesLoadedUtc = DateTime.MinValue;
    private static readonly TimeSpan AuthoritiesTtl = TimeSpan.FromSeconds(60);
    private readonly object _authoritiesLock = new();

    public AppTokenTrust(
        IServiceScopeFactory scopeFactory,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AppTokenTrust> logger)
    {
        _scopeFactory = scopeFactory;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    private bool IsOwnIssuer(string? issuer) =>
        string.Equals(issuer, _jwtSettings.Issuer, StringComparison.Ordinal);

    /// <summary>Issuer is valid when it is ByteAI's own or a registered active app's authority.</summary>
    public string ValidateIssuer(string issuer, SecurityToken token, TokenValidationParameters parameters)
    {
        if (IsOwnIssuer(issuer) || Authorities().ContainsKey(issuer.TrimEnd('/')))
            return issuer;
        throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' is not trusted.")
        {
            InvalidIssuer = issuer
        };
    }

    /// <summary>Own issuer → the symmetric secret; app authority → its discovered JWKS keys.</summary>
    public IEnumerable<SecurityKey> ResolveSigningKeys(
        string token, SecurityToken securityToken, string kid, TokenValidationParameters parameters)
    {
        var issuer = securityToken.Issuer?.TrimEnd('/');

        if (issuer is null || IsOwnIssuer(securityToken.Issuer))
            return new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret)) };

        if (!Authorities().TryGetValue(issuer, out var appCode))
            return Array.Empty<SecurityKey>();

        try
        {
            var manager = _oidcConfigs.GetOrAdd(issuer, a =>
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{a}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = false }));

            var config = manager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult();
            return config.SigningKeys;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load OIDC signing keys for app '{App}' from {Issuer}.", appCode, issuer);
            return Array.Empty<SecurityKey>();
        }
    }

    /// <summary>Audience is enforced only for ByteAI's own tokens; registered apps own their audiences.</summary>
    public bool ValidateAudience(
        IEnumerable<string> audiences, SecurityToken securityToken, TokenValidationParameters parameters)
    {
        if (!IsOwnIssuer(securityToken.Issuer))
            return true;
        return audiences.Any(a => string.Equals(a, _jwtSettings.Audience, StringComparison.Ordinal));
    }

    // Active app authorities, refreshed at most once per minute.
    private IReadOnlyDictionary<string, string> Authorities()
    {
        if (DateTime.UtcNow - _authoritiesLoadedUtc < AuthoritiesTtl && _authorities is not null)
            return _authorities;

        lock (_authoritiesLock)
        {
            if (DateTime.UtcNow - _authoritiesLoadedUtc < AuthoritiesTtl && _authorities is not null)
                return _authorities;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                _authorities = db.Apps.AsNoTracking()
                    .Where(a => a.IsActive && a.JwtAuthority != null)
                    .Select(a => new { a.JwtAuthority, a.Code })
                    .ToList()
                    .ToDictionary(a => a.JwtAuthority!.TrimEnd('/'), a => a.Code, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load registered app authorities; keeping the previous set.");
                _authorities ??= new Dictionary<string, string>();
            }
            _authoritiesLoadedUtc = DateTime.UtcNow;
            return _authorities;
        }
    }
}
