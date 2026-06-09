using System.Globalization;

namespace Web.Services;

public class JsonStringLocalizerFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private JsonStringLocalizer? _currentLocalizer;
    private string? _currentCulture;
    private Task? _loadingTask;

    public JsonStringLocalizerFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IJsonStringLocalizer> CreateAsync()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        
        // Reuse localizer if culture hasn't changed
        if (_currentLocalizer != null && _currentCulture == culture)
        {
            return _currentLocalizer;
        }

        var httpClient = _httpClientFactory.CreateClient("LocalizationClient");
        _currentLocalizer = new JsonStringLocalizer(culture);
        await _currentLocalizer.LoadAsync(httpClient);
        _currentCulture = culture;
        
        return _currentLocalizer;
    }

    public IJsonStringLocalizer CreateSync()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        
        // Return existing or create a new one (will load on first use)
        if (_currentLocalizer != null && _currentCulture == culture)
        {
            return _currentLocalizer;
        }

        _currentLocalizer = new JsonStringLocalizer(culture);
        _currentCulture = culture;
        
        // Start loading in background if not already loading
        if (_loadingTask == null || _loadingTask.IsCompleted)
        {
            var httpClient = _httpClientFactory.CreateClient("LocalizationClient");
            _loadingTask = _currentLocalizer.LoadAsync(httpClient);
        }
        
        return _currentLocalizer;
    }
}
