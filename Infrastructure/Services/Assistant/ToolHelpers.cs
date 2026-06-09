using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// Shared, code-owned helpers for the read tools: named-period resolution, AED
/// money formatting, and a JSON sanitizer that strips GUIDs/ID fields and runs the
/// PII redactor over every tool payload before it reaches the model. Keeping these
/// here means each tool stays a few lines and the "no IDs / no PII to the model"
/// guarantees are enforced in ONE place.
/// </summary>
internal static class ToolHelpers
{
    private static readonly CultureInfo Money = CultureInfo.GetCultureInfo("en-US");

    private static readonly Regex GuidPattern = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    /// <summary>The named time ranges the period argument accepts.</summary>
    public static readonly string[] PeriodNames =
    {
        "today", "yesterday", "thisWeek", "thisMonth", "lastMonth",
        "last7Days", "last30Days", "thisYear"
    };

    /// <summary>A reusable JSON-schema fragment for an optional period argument.</summary>
    public static object PeriodParam(string description) => new
    {
        type = "string",
        @enum = PeriodNames,
        description
    };

    /// <summary>Format a decimal as "AED 12,345.67" (thousand separators, 2 dp).</summary>
    public static string Aed(decimal amount) => "AED " + amount.ToString("N2", Money);

    /// <summary>A human-readable label for a period token (or "all time").</summary>
    public static string PeriodLabel(string? period) =>
        string.IsNullOrWhiteSpace(period) ? "all time" : period!;

    /// <summary>Resolve a named period token to an inclusive UTC [from, to] range.</summary>
    public static (DateTime? from, DateTime? to) ResolvePeriod(string? period)
    {
        if (string.IsNullOrWhiteSpace(period)) return (null, null);

        var today = DateTime.UtcNow.Date;
        return period.Trim().ToLowerInvariant() switch
        {
            "today" => (today, today.AddDays(1).AddTicks(-1)),
            "yesterday" => (today.AddDays(-1), today.AddTicks(-1)),
            "thisweek" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(7 - (int)today.DayOfWeek).AddTicks(-1)),
            "thismonth" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1).AddTicks(-1)),
            "lastmonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddTicks(-1)),
            "last7days" => (today.AddDays(-7), today.AddDays(1).AddTicks(-1)),
            "last30days" => (today.AddDays(-30), today.AddDays(1).AddTicks(-1)),
            "thisyear" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year + 1, 1, 1).AddTicks(-1)),
            _ => (null, null)
        };
    }

    /// <summary>Normalise a requested list size to a sane range (default 10, max 50).</summary>
    public static int ClampLimit(int? n) => n is null or <= 0 ? 10 : Math.Min(n.Value, 50);

    // Period phrases (EN + AR/Gulf), most-specific first, mapped to a period token.
    private static readonly (string Token, string[] Phrases)[] PeriodPhrases =
    {
        ("last7Days",  new[] { "last 7 days", "past 7 days", "last seven days", "آخر 7 أيام", "اخر 7 ايام", "آخر سبعة أيام", "آخر أسبوع", "اخر اسبوع" }),
        ("last30Days", new[] { "last 30 days", "past 30 days", "last thirty days", "آخر 30 يوم", "اخر 30 يوم", "آخر ثلاثين يوم" }),
        ("yesterday",  new[] { "yesterday", "أمس", "امس", "البارحة", "امبارح" }),
        ("lastMonth",  new[] { "last month", "previous month", "الشهر الماضي", "الشهر السابق", "الشهر اللي فات", "قبل شهر" }),
        ("thisWeek",   new[] { "this week", "current week", "هذا الأسبوع", "هالاسبوع", "هالأسبوع" }),
        ("thisMonth",  new[] { "this month", "current month", "هذا الشهر", "هالشهر" }),
        ("thisYear",   new[] { "this year", "year to date", "ytd", "هذا العام", "هذه السنة", "هالسنة" }),
        ("today",      new[] { "today", "اليوم", "النهاردة" }),
    };

    /// <summary>
    /// Deterministically detect a named period token in a question (EN/AR), or null.
    /// Used so an "extract" period parameter doesn't depend on the LLM for the common
    /// phrasings ("last month" → lastMonth).
    /// </summary>
    public static string? DetectPeriodToken(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;
        var q = question.ToLowerInvariant();
        foreach (var (token, phrases) in PeriodPhrases)
            if (phrases.Any(p => q.Contains(p)))
                return token;
        return null;
    }

    private static readonly JsonSerializerOptions SerializeOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Serialize a tool result to a model-safe JSON string: strip GUID/ID fields and
    /// system identifiers, run the PII redactor, then cap the length. This is the
    /// single choke point that keeps IDs and sensitive scalars out of the model.
    /// </summary>
    public static string ToModelJson(object? data, int maxChars)
    {
        if (data is null) return "{}";

        var node = JsonSerializer.SerializeToNode(data, SerializeOpts);
        Sanitize(node);
        SensitiveDataRedactor.Redact(node);

        var json = node?.ToJsonString(SerializeOpts) ?? "{}";
        return json.Length > maxChars ? json[..maxChars] + "...[truncated]" : json;
    }

    /// <summary>Recursively drop ID/GUID-valued fields so no identifier reaches the model.</summary>
    private static void Sanitize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kvp in obj.ToList())
                {
                    if (IsIdKey(kvp.Key) || IsGuidValue(kvp.Value))
                        obj.Remove(kvp.Key);
                    else
                        Sanitize(kvp.Value);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr)
                    Sanitize(item);
                break;
        }
    }

    // camelCase keys: drop "id", "...Id", "...Ids" (capital I marks the PascalCase
    // boundary, so "paid"/"void" are not affected).
    private static bool IsIdKey(string key) =>
        key.Equals("id", StringComparison.OrdinalIgnoreCase)
        || key.EndsWith("Id", StringComparison.Ordinal)
        || key.EndsWith("Ids", StringComparison.Ordinal);

    private static bool IsGuidValue(JsonNode? value) =>
        value is JsonValue v
        && v.TryGetValue<string>(out var s)
        && s is not null
        && GuidPattern.IsMatch(s);
}
