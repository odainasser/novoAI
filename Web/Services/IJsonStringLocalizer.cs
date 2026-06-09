namespace Web.Services;

public interface IJsonStringLocalizer
{
    string this[string name] { get; }
    string this[string name, params object[] arguments] { get; }
    string GetString(string name);
    string GetString(string name, params object[] arguments);
}
