namespace Application.Common.Interfaces;

public interface IAppConfiguration
{
    string GetAppUrl();
    string? GetValue(string key);
}
