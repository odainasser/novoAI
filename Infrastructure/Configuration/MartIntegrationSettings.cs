namespace Infrastructure.Configuration;

/// <summary>
/// Connection settings for the ByteMart tool provider — the system that owns the
/// business data and the code-owned read tools the assistant executes.
/// </summary>
public class MartIntegrationSettings
{
    public string BaseUrl { get; set; } = "http://localhost:5050";

    /// <summary>How long a fetched tool catalog snapshot is served before refreshing.</summary>
    public int CatalogCacheSeconds { get; set; } = 300;

    public int TimeoutSeconds { get; set; } = 60;
}
