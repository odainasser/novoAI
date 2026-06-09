using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Configuration;

public class AppConfiguration : IAppConfiguration
{
    private readonly IConfiguration _configuration;

    public AppConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetAppUrl()
    {
        return _configuration["AppUrl"]
            ?? throw new InvalidOperationException("AppUrl is not configured in appsettings.json");
    }

    public string? GetValue(string key)
    {
        return _configuration[key];
    }
}
