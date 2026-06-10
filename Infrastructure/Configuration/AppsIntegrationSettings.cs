namespace Infrastructure.Configuration;

/// <summary>
/// Cross-app settings for the Apps integration module. Per-app data (base URL,
/// persona, currency) lives in the Apps table — registered through the admin UI,
/// never in configuration.
/// </summary>
public class AppsIntegrationSettings
{
    /// <summary>How long a fetched tool catalog snapshot is served before refreshing.</summary>
    public int CatalogCacheSeconds { get; set; } = 300;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Optional per-issuer signing-key sources. Lets a registered app's tokens be
    /// validated when the issuer URL embedded in the token (and in the app's OIDC
    /// metadata) is NOT reachable from where novoAI runs — e.g. Novologs issues
    /// tokens with <c>iss=http://tenant:8080</c> (a docker-internal name) while
    /// novoAI runs standalone and can only reach the published
    /// <c>http://localhost:5001/.well-known/jwks.json</c>. When a token's issuer
    /// matches <see cref="TokenKeySource.Issuer"/>, signing keys are fetched
    /// directly from <see cref="TokenKeySource.JwksUri"/> instead of via OIDC
    /// discovery. Issuer-string validation is unchanged.
    /// </summary>
    public List<TokenKeySource> TokenKeySources { get; set; } = new();
}

/// <summary>A reachable JWKS endpoint for a specific (possibly unreachable) token issuer.</summary>
public class TokenKeySource
{
    /// <summary>The exact <c>iss</c> value carried by the app's tokens (e.g. http://tenant:8080).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>A network-reachable JWKS URL serving that issuer's signing keys.</summary>
    public string JwksUri { get; set; } = string.Empty;
}
