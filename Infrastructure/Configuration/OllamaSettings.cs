namespace Infrastructure.Configuration;

public class OllamaSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5:14b";
    public int MaxToolIterations { get; set; } = 5;
    public int MaxHistoryMessages { get; set; } = 20;
    public int MaxToolResultChars { get; set; } = 4000;
    public int TotalTimeoutSeconds { get; set; } = 90;
    public int TimeoutSeconds { get; set; } = 120;
    public int CacheResultSeconds { get; set; } = 30;
}
