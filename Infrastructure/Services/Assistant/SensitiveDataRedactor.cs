using System.Text.Json.Nodes;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// Scrubs PII / sensitive scalar values out of an arbitrary JSON tree before it
/// is handed to the LLM for answer phrasing. Aggregate business figures (counts,
/// totals, names of products, …) are preserved — only fields whose property name
/// matches a sensitive fragment (email, phone, address, …) are replaced.
/// </summary>
public static class SensitiveDataRedactor
{
    public const string RedactedPlaceholder = "[redacted]";

    private static readonly string[] SensitiveKeyFragments =
    {
        "email", "phone", "mobile", "password", "secret", "token",
        "contactperson", "address"
    };

    /// <summary>Recursively redacts sensitive scalar fields in place.</summary>
    public static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kvp in obj.ToList())
                {
                    if (kvp.Value is JsonValue && IsSensitiveKey(kvp.Key))
                        obj[kvp.Key] = RedactedPlaceholder;
                    else
                        Redact(kvp.Value);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    Redact(item);
                break;
        }
    }

    public static bool IsSensitiveKey(string key)
    {
        foreach (var fragment in SensitiveKeyFragments)
            if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
