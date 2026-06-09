using System.Globalization;
using System.Text.Json;

namespace Web.Services;

public class JsonStringLocalizer : IJsonStringLocalizer
{
    private Dictionary<string, string> _localizations = new();
    private readonly string _currentCulture;
    private bool _isLoaded = false;

    public JsonStringLocalizer(string culture)
    {
        _currentCulture = culture ?? CultureInfo.CurrentUICulture.Name;
    }

    public string this[string name] => GetString(name);

    public string this[string name, params object[] arguments] => GetString(name, arguments);

    public string GetString(string name)
    {
        return _localizations.TryGetValue(name, out var value) ? value : name;
    }

    public string GetString(string name, params object[] arguments)
    {
        var format = GetString(name);
        return arguments != null && arguments.Length > 0 
            ? string.Format(format, arguments) 
            : format;
    }

    public async Task LoadAsync(HttpClient httpClient)
    {
        if (_isLoaded)
            return;

        try
        {
            var cultureName = _currentCulture.Split('-')[0];
            var jsonFilePath = $"localization/{cultureName}.json";
            
            var json = await httpClient.GetStringAsync(jsonFilePath);
            
            if (!string.IsNullOrEmpty(json))
            {
                var localizations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (localizations != null)
                {
                    _localizations = localizations;
                }
            }
            
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading localization file: {ex.Message}");
            _localizations = new Dictionary<string, string>();
            _isLoaded = true;
        }
    }
}
