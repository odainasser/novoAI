using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Services;

public class OllamaClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<OllamaChatResponse> ChatAsync(
        string model,
        List<OllamaChatMessage> messages,
        List<object>? tools,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Ollama");

        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Tools = tools?.Count > 0 ? tools : null,
            Stream = false
        };

        var response = await client.PostAsJsonAsync("api/chat", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, cancellationToken);
        return result ?? new OllamaChatResponse();
    }

    public class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaChatMessage> Messages { get; set; } = new();
        public List<object>? Tools { get; set; }
        public bool Stream { get; set; }
    }

    public class OllamaChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("tool_calls")]
        public List<OllamaToolCall>? ToolCalls { get; set; }
    }

    public class OllamaToolCall
    {
        public OllamaToolCallFunction Function { get; set; } = new();
    }

    public class OllamaToolCallFunction
    {
        public string Name { get; set; } = string.Empty;
        public JsonElement Arguments { get; set; }
    }

    public class OllamaChatResponse
    {
        public OllamaChatMessage? Message { get; set; }
    }
}
