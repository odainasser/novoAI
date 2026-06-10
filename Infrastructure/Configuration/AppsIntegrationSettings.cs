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
}
