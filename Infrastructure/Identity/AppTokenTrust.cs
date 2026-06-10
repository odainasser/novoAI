using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
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
    private readonly AppsIntegrationSettings _appsSettings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AppTokenTrust> _logger;

    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _oidcConfigs = new(StringComparer.OrdinalIgnoreCase);

    // Directly-fetched JWKS keys (for issuers whose own metadata URL is unreachable),
    // cached per issuer and refreshed on a short TTL like the OIDC configs.
    private readonly ConcurrentDictionary<string, (DateTime FetchedUtc, IReadOnlyCollection<SecurityKey> Keys)> _jwksCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan JwksTtl = TimeSpan.FromMinutes(10);

    private IReadOnlyDictionary<string, string>? _authorities;   // issuer -> app code
    private DateTime _authoritiesLoadedUtc = DateTime.MinValue;
    private static readonly TimeSpan AuthoritiesTtl = TimeSpan.FromSeconds(60);
    private readonly object _authoritiesLock = new();

    public AppTokenTrust(
        IServiceScopeFactory scopeFactory,
        IOptions<JwtSettings> jwtSettings,
        IOptions<AppsIntegrationSettings> appsSettings,
        IHttpClientFactory httpClientFactory,
        ILogger<AppTokenTrust> logger)
    {
        _scopeFactory = scopeFactory;
        _jwtSettings = jwtSettings.Value;
        _appsSettings = appsSettings.Value;
        _httpClientFactory = httpClientFactory;
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

        // When a reachable JWKS source is configured for this issuer, fetch keys
        // directly from it — the issuer's own metadata URL may be unreachable from
        // where novoAI runs (e.g. a docker-internal `iss`).
        var overrideKeys = ResolveOverrideKeys(issuer, appCode);
        if (overrideKeys is not null)
            return overrideKeys;

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

    /// <summary>
    /// Signing keys for an issuer that has a configured reachable JWKS source, fetched
    /// directly from that URL (no OIDC discovery) and cached on a short TTL. Returns
    /// null when no source is configured for the issuer, so the caller falls back to
    /// OIDC discovery.
    /// </summary>
    private IReadOnlyCollection<SecurityKey>? ResolveOverrideKeys(string issuer, string appCode)
    {
        var source = _appsSettings.TokenKeySources?
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.JwksUri)
                && string.Equals(s.Issuer?.TrimEnd('/'), issuer, StringComparison.OrdinalIgnoreCase));
        if (source is null)
            return null;

        if (_jwksCache.TryGetValue(issuer, out var cached) && DateTime.UtcNow - cached.FetchedUtc < JwksTtl)
            return cached.Keys;

        try
        {
            var client = _httpClientFactory.CreateClient("AppTools");
            var json = client.GetStringAsync(source.JwksUri).GetAwaiter().GetResult();
            IReadOnlyCollection<SecurityKey> keys = new JsonWebKeySet(json).GetSigningKeys().ToList();
            _jwksCache[issuer] = (DateTime.UtcNow, keys);
            return keys;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch JWKS for app '{App}' from {JwksUri}.", appCode, source.JwksUri);
            // Serve a stale snapshot if we have one rather than rejecting every token.
            return _jwksCache.TryGetValue(issuer, out var stale) ? stale.Keys : null;
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
