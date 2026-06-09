using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Application.Features.Assistant;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Configuration;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// The governed-plan runtime: Call 1 classifies the question into a match key, a
/// confirmed <see cref="AssistantPlan"/> is matched, and its recipe is executed
/// deterministically â€” parameters resolved (static / context / a small LLM extract),
/// tools run via the catalog (each permission- and branch-gated), results joined in
/// code, and the output shaped per the plan. The model never decides what to fetch
/// and never does cross-record math. When nothing matches, the caller falls back to
/// live tool-calling.
/// </summary>
internal sealed class AssistantPlanEngine
{
    private readonly OllamaClient _ollama;
    private readonly OllamaSettings _settings;
    private readonly ToolCatalog _catalog;
    private readonly ILogger<AssistantPlanEngine> _logger;

    private static readonly Regex JsonBlock = new(@"\{[\s\S]*\}", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] Actions =
        { "count", "sum", "list", "compare", "top", "detail", "status" };

    public AssistantPlanEngine(
        OllamaClient ollama,
        IOptions<OllamaSettings> options,
        ToolCatalog catalog,
        ILogger<AssistantPlanEngine> logger)
    {
        _ollama = ollama;
        _settings = options.Value;
        _catalog = catalog;
        _logger = logger;
    }

    public async Task<PlanExecutionResult> TryExecuteAsync(string question, string locale, ToolContext ctx)
    {
        // Call 1 â€” classify the question into a match key.
        var match = await ClassifyAsync(question, ctx.Ct);
        var classifyEvidence = ClassifyEvidence(match);
        if (match is null || !match.IsUsable)
            return PlanExecutionResult.NotMatched(classifyEvidence);

        // Coverage: is the question's area/entity served by ANY tool? If not, it's an
        // honest "not supported" â€” a no-answer with a precise, code-set reason. The
        // classifier names the real subject (even one outside the catalog) so a question
        // about, say, customers or tax surfaces here instead of being silently mis-mapped.
        var coveredDomains = match.Domains.Select(CanonicalDomain).OfType<string>().Distinct().ToList();
        if (coveredDomains.Count == 0)
            return PlanExecutionResult.NoAnswer(Unsupported(locale), NoAnswerReason.UnsupportedDomain,
                $"{{\"classify\":{classifyEvidence},\"unsupported\":\"domain\"}}");

        var canonEntity = CanonicalEntity(match.Entity);
        if (canonEntity is null)
            return PlanExecutionResult.NoAnswer(Unsupported(locale), NoAnswerReason.UnsupportedEntity,
                $"{{\"classify\":{classifyEvidence},\"unsupported\":\"entity\"}}");

        // A mixing question whose SECOND dataset isn't covered is also unsupported.
        if (match.SecondaryEntity is not null)
        {
            var canonSecondary = CanonicalEntity(match.SecondaryEntity);
            if (canonSecondary is null)
                return PlanExecutionResult.NoAnswer(Unsupported(locale), NoAnswerReason.UnsupportedEntity,
                    $"{{\"classify\":{classifyEvidence},\"unsupported\":\"secondaryEntity\"}}");
            match.SecondaryEntity = canonSecondary;
        }

        // Canonicalize to the catalog's exact spellings so the plan match key lines up.
        match.Domains = coveredDomains;
        match.Entity = canonEntity;

        // Match a confirmed plan (highest version, then success score).
        var db = ctx.Sp.GetRequiredService<ApplicationDbContext>();
        var key = match.Key();
        var plan = await db.Set<AssistantPlan>()
            .Where(p => p.Status == PlanStatus.Confirmed && p.MatchKey == key)
            .OrderByDescending(p => p.Version).ThenByDescending(p => p.SuccessScore)
            .FirstOrDefaultAsync(ctx.Ct);
        if (plan is null)
            return PlanExecutionResult.NotMatched(classifyEvidence);

        PlanDefinition def;
        try { def = JsonSerializer.Deserialize<PlanDefinition>(plan.DefinitionJson, Camel) ?? new(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Plan {Id} has an unreadable definition; falling back.", plan.Id); return PlanExecutionResult.NotMatched(classifyEvidence); }

        if (def.Tools.Count == 0)
            return PlanExecutionResult.NotMatched(classifyEvidence);

        // Security: explicit required permissions (both entities for mixing).
        // A permission refusal is an honest answer, not a no-answer.
        foreach (var perm in def.RequiredPermissions)
            if (!ctx.Permissions.Contains(perm))
                return PlanExecutionResult.Direct(PermissionDenied(locale));

        // Validate every tool is real and usable in this caller's context.
        var tools = new Dictionary<string, IAssistantTool>();
        foreach (var pt in def.Tools)
        {
            var tool = _catalog.Find(pt.Name);
            if (tool is null)
            {
                _logger.LogWarning("Plan {Id} references unknown tool '{Tool}'; falling back.", plan.Id, pt.Name);
                return PlanExecutionResult.NotMatched(classifyEvidence);
            }
            // Branch-scope and permission refusals are honest answers, not no-answers.
            if (ctx.BranchLocked && tool.CrossBranch)
                return PlanExecutionResult.Direct(OutOfScope(locale));
            if (!ToolCatalog.CanUse(tool, ctx))
                return PlanExecutionResult.Direct(PermissionDenied(locale));
            tools[pt.Id] = tool;
        }

        // Resolve "extract" parameters in one bounded LLM call.
        var extracted = await ExtractAsync(question, def, ctx.Ct);

        // Missing required parameter â†’ ask the user, don't run unfiltered.
        foreach (var pt in def.Tools)
        {
            var required = ToolSchema.Params(tools[pt.Id].ParametersSchema)
                .Where(s => s.Required).Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var pp in pt.Params)
            {
                var isRequired = pp.Required || required.Contains(pp.Name);
                if (!string.Equals(pp.Source, "extract", StringComparison.OrdinalIgnoreCase) || !isRequired)
                    continue;
                var ph = (pp.Placeholder ?? pp.Name).Trim().Trim('{', '}');
                if (!extracted.TryGetValue(ph, out var v) || string.IsNullOrWhiteSpace(v))
                    return PlanExecutionResult.NoAnswer(MissingParam(locale, pp.Name), NoAnswerReason.MissingParameter,
                        $"{{\"tool\":\"{pt.Name}\",\"param\":\"{pp.Name}\",\"required\":true,\"plan\":\"{key}\"}}");
            }
        }

        // Execute the tools in order; keep raw (un-stripped) results for joining.
        var rawByTool = new Dictionary<string, JsonNode?>();
        foreach (var pt in def.Tools)
        {
            var args = BuildArgs(pt, extracted, ctx);
            var result = await tools[pt.Id].ExecuteAsync(args, ctx);
            rawByTool[pt.Id] = result.Data is null ? null : JsonSerializer.SerializeToNode(result.Data, Camel);
        }

        // Combine (join in code) and shape the output per the plan's hints.
        var rows = Combine(def, rawByTool);
        var payload = Shape(def.Output, rows, rawByTool, def);

        // Template path â†’ deterministic render, no phrasing call.
        string? direct = TryTemplate(def.Output.TemplateId, payload, locale);

        // Best-effort usage bump.
        try { plan.UsageCount++; await db.SaveChangesAsync(ctx.Ct); } catch { /* never block answering */ }

        var hasData = HasRows(payload);
        return new PlanExecutionResult
        {
            Matched = true,
            HasData = hasData,
            Data = direct is null ? payload : null,
            DirectAnswer = direct,
            ToolsUsed = def.Tools.Select(t => t.Name).ToList(),
            IsMixing = def.Tools.Count > 1 || !string.IsNullOrWhiteSpace(plan.SecondaryEntity),
            // A plan that ran but returned nothing is a valid "there are none" — an honest
            // answer (no reason, not a no-answer); stated deterministically by the orchestrator.
            Reason = null,
            Evidence = null
        };
    }

    private static string ClassifyEvidence(PlanMatch? m) => m is null
        ? "{\"classified\":null}"
        : "{\"domains\":[" + string.Join(",", m.Domains.Select(d => $"\"{d}\"")) + "]," +
          $"\"action\":\"{m.Action}\",\"entity\":\"{m.Entity}\"," +
          $"\"secondaryEntity\":{(m.SecondaryEntity is null ? "null" : $"\"{m.SecondaryEntity}\"")}}}";

    private static string MissingParam(string locale, string name) =>
        locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? $"Ø§Ù„Ø±Ø¬Ø§Ø¡ ØªØ­Ø¯ÙŠØ¯ \"{name}\" Ø­ØªÙ‰ Ø£ØªÙ…ÙƒÙ† Ù…Ù† Ø§Ù„Ø¥Ø¬Ø§Ø¨Ø©."
            : $"Please specify \"{name}\" so I can answer.";

    // â”€â”€ Call 1: classification â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<PlanMatch?> ClassifyAsync(string question, CancellationToken ct)
    {
        var domains = _catalog.All.Select(t => t.Domain).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d);
        var entities = _catalog.All.SelectMany(t => t.Entities).Distinct().OrderBy(e => e);

        var system =
            "Classify the retail business question into a compact JSON object and output ONLY that JSON.\n" +
            "{\"domains\":[1-2 business areas],\n" +
            " \"action\": one of [" + string.Join(", ", Actions) + "],\n" +
            " \"entity\": the main business entity the question is about,\n" +
            " \"secondaryEntity\": a second entity ONLY if the question needs two datasets, else null}\n" +
            "Prefer these exact names when they fit — areas: [" + string.Join(", ", domains) + "]; " +
            "entities: [" + string.Join(", ", entities) + "].\n" +
            "If the question is about an area or entity NOT in those lists (e.g. customers, tax, payroll), " +
            "use the most accurate real word anyway — do not force-fit. No prose, no markdown.";

        try
        {
            var resp = await _ollama.ChatAsync(_settings.Model, new()
            {
                new() { Role = "system", Content = system },
                new() { Role = "user", Content = $"Question: {question}\nReturn only the JSON." }
            }, tools: null, ct);

            var json = ExtractJson(resp.Message?.Content);
            if (json is null) return null;

            var raw = JsonSerializer.Deserialize<RawMatch>(json, Camel);
            if (raw is null) return null;

            return new PlanMatch
            {
                Domains = (raw.Domains ?? new()).Where(d => !string.IsNullOrWhiteSpace(d)).Take(2).ToList(),
                Action = Clean(raw.Action),
                Entity = Clean(raw.Entity),
                SecondaryEntity = Clean(raw.SecondaryEntity)
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plan classification failed; falling back to tool-calling.");
            return null;
        }
    }

    private sealed class RawMatch
    {
        public List<string>? Domains { get; set; }
        public string? Action { get; set; }
        public string? Entity { get; set; }
        public string? SecondaryEntity { get; set; }
    }

    // â”€â”€ Call 2: bounded extraction of declared placeholders â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<IReadOnlyDictionary<string, string?>> ExtractAsync(string question, PlanDefinition def, CancellationToken ct)
    {
        var fields = def.Tools.SelectMany(t => t.Params)
            .Where(p => string.Equals(p.Source, "extract", StringComparison.OrdinalIgnoreCase))
            .Select(p => Placeholder(p))
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct()
            .ToList();

        if (fields.Count == 0)
            return new Dictionary<string, string?>();

        // A "period" field is resolved deterministically (EN/AR phrase â†’ token) â€” no
        // dependence on the model for the common "last month / this week" cases.
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var periodFields = fields.Where(IsPeriodField).ToList();
        foreach (var pf in periodFields)
            result[pf] = ToolHelpers.DetectPeriodToken(question);

        var llmFields = fields.Where(f => !IsPeriodField(f)).ToList();
        if (llmFields.Count == 0)
            return result;   // everything was deterministic â€” skip the model call

        var system =
            "Extract the requested fields from the user's question. Output ONLY a JSON object with exactly these keys: " +
            string.Join(", ", llmFields) + ". If a field is not present in the question, use null. No prose.";

        try
        {
            var resp = await _ollama.ChatAsync(_settings.Model, new()
            {
                new() { Role = "system", Content = system },
                new() { Role = "user", Content = $"Question: {question}" }
            }, tools: null, ct);

            var json = ExtractJson(resp.Message?.Content);
            var node = json is null ? null : JsonNode.Parse(json) as JsonObject;
            if (node is not null)
                foreach (var f in llmFields)
                    result[f] = node.TryGetPropertyValue(f, out var v) ? v?.ToString() : null;
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plan parameter extraction failed; running with deterministic extracts only.");
            return result;   // keep any deterministic period already resolved
        }
    }

    private static string Placeholder(PlanParam p) =>
        (p.Placeholder ?? p.Name).Trim().Trim('{', '}');

    private static bool IsPeriodField(string field) =>
        field.Equals("period", StringComparison.OrdinalIgnoreCase);

    // â”€â”€ Argument assembly (static / context / extract) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static JsonElement BuildArgs(PlanTool tool, IReadOnlyDictionary<string, string?> extracted, ToolContext ctx)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var p in tool.Params)
        {
            object? value = p.Source.ToLowerInvariant() switch
            {
                "static" => p.Value,
                "context" => ContextValue(p.ContextKey, ctx),   // identity/branch from context ONLY
                "extract" => extracted.TryGetValue(Placeholder(p), out var v) ? v : null,
                _ => null
            };
            if (value is not null && !(value is string s && string.IsNullOrWhiteSpace(s)))
                dict[p.Name] = value;
        }
        return JsonSerializer.SerializeToElement(dict, Camel);
    }

    private static string? ContextValue(string? key, ToolContext ctx) => (key ?? "").Trim().ToLowerInvariant() switch
    {
        "userid" => ctx.UserId,
        "branchid" => ctx.BranchId?.ToString(),
        _ => null
    };

    // â”€â”€ Combine (join in code) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static JsonArray Combine(PlanDefinition def, IReadOnlyDictionary<string, JsonNode?> rawByTool)
    {
        if (def.Tools.Count == 1)
            return RowsOf(rawByTool[def.Tools[0].Id]);

        if (def.Joins.Count == 0)
            return RowsOf(rawByTool[def.Tools[0].Id]);

        // Start from the first join's left dataset; fold each step into the working set.
        var working = RowsOf(rawByTool.GetValueOrDefault(def.Joins[0].Left));
        foreach (var join in def.Joins)
        {
            var right = RowsOf(rawByTool.GetValueOrDefault(join.Right));
            working = JoinRows(working, right, join.On);
        }
        return working;
    }

    private static JsonArray JoinRows(JsonArray left, JsonArray right, List<PlanJoinKey> on)
    {
        var merged = new JsonArray();
        foreach (var l in left.OfType<JsonObject>())
        {
            foreach (var r in right.OfType<JsonObject>())
            {
                if (on.All(k => KeyEquals(l, k.LeftKey, r, k.RightKey)))
                {
                    var row = new JsonObject();
                    foreach (var kv in l) row[kv.Key] = kv.Value?.DeepClone();
                    foreach (var kv in r) if (!row.ContainsKey(kv.Key)) row[kv.Key] = kv.Value?.DeepClone();
                    merged.Add(row);
                }
            }
        }
        return merged;
    }

    private static bool KeyEquals(JsonObject l, string lk, JsonObject r, string rk)
    {
        var lv = Prop(l, lk);
        var rv = Prop(r, rk);
        return lv is not null && rv is not null &&
               string.Equals(lv.ToString(), rv.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static JsonNode? Prop(JsonObject o, string name)
    {
        foreach (var kv in o)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        return null;
    }

    // The list a tool result exposes: items / rows / data, or the node if it's an array.
    private static JsonArray RowsOf(JsonNode? node)
    {
        if (node is JsonArray arr) return arr;
        if (node is JsonObject obj)
        {
            foreach (var name in new[] { "items", "rows", "data" })
                if (Prop(obj, name) is JsonArray a)
                    return a;
        }
        return new JsonArray();
    }

    // â”€â”€ Output shaping (precompute totals, cap, links) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static object Shape(PlanOutput output, JsonArray rows, IReadOnlyDictionary<string, JsonNode?> rawByTool, PlanDefinition def)
    {
        // Single-tool plan with no joinable list: hand back the raw object as-is.
        if (rows.Count == 0 && def.Tools.Count == 1 && def.Joins.Count == 0)
        {
            var only = rawByTool[def.Tools[0].Id];
            if (only is JsonObject obj && RowsOf(obj).Count == 0)
                return only;
        }

        var totals = new JsonObject();
        foreach (var spec in output.PrecomputeTotals)
        {
            if (string.Equals(spec, "count", StringComparison.OrdinalIgnoreCase))
                totals["count"] = rows.Count;
            else if (spec.StartsWith("sum:", StringComparison.OrdinalIgnoreCase))
            {
                var field = spec[4..];
                decimal sum = 0;
                foreach (var row in rows.OfType<JsonObject>())
                    if (Prop(row, field) is JsonValue v && v.TryGetValue<decimal>(out var d)) sum += d;
                totals["sum_" + field] = sum;
            }
        }

        var total = rows.Count;
        var cap = output.RowCap is > 0 ? output.RowCap.Value : 25;
        var shown = new JsonArray();
        var linkPattern = output.LinkRoute.Values.FirstOrDefault();
        foreach (var row in rows.OfType<JsonObject>().Take(cap))
        {
            var clone = (JsonObject)row.DeepClone();
            if (!string.IsNullOrWhiteSpace(linkPattern) && Prop(clone, "id") is JsonValue idv)
                clone["link"] = linkPattern!.Replace("{id}", idv.ToString());
            shown.Add(clone);
        }

        return new JsonObject
        {
            ["totals"] = totals,
            ["total"] = total,
            ["shown"] = shown.Count,
            ["truncated"] = total > shown.Count,
            ["rows"] = shown
        };
    }

    private static bool HasRows(object payload)
    {
        if (payload is JsonObject o)
        {
            if (Prop(o, "rows") is JsonArray a) return a.Count > 0;
            if (Prop(o, "total") is JsonValue t && t.TryGetValue<int>(out var n)) return n > 0;
            return o.Count > 0;
        }
        return payload is not null;
    }

    // â”€â”€ Template path (0 LLM calls) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static string? TryTemplate(string? templateId, object payload, string locale)
    {
        if (string.IsNullOrWhiteSpace(templateId) || payload is not JsonObject o) return null;
        var ar = locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(templateId, "count", StringComparison.OrdinalIgnoreCase))
        {
            var n = (Prop(o, "total") as JsonValue)?.GetValue<int>() ?? 0;
            return ar ? $"Ø§Ù„Ø¹Ø¯Ø¯: {n:N0}." : $"Count: {n:N0}.";
        }
        return null; // unknown template â†’ fall through to phrasing
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static string? ExtractJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var m = JsonBlock.Match(content);
        return m.Success ? m.Value : null;
    }

    private static string? Clean(string? s)
    {
        var t = s?.Trim();
        return string.IsNullOrWhiteSpace(t) || t.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : t;
    }

    private static string PermissionDenied(string locale) =>
        locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? "Ø¹Ø°Ø±Ø§Ù‹ØŒ Ù„Ø§ ØªÙ…Ù„Ùƒ Ø§Ù„ØµÙ„Ø§Ø­ÙŠØ© Ù„Ù„ÙˆØµÙˆÙ„ Ø¥Ù„Ù‰ Ù‡Ø°Ù‡ Ø§Ù„Ø¨ÙŠØ§Ù†Ø§Øª."
            : "Sorry, you don't have permission to access this data.";

    private static string OutOfScope(string locale) =>
        locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? "Ù‡Ø°Ø§ Ø§Ù„Ù…Ø³Ø§Ø¹Ø¯ Ù…Ø®ØµØµ Ù„ÙØ±Ø¹Ùƒ Ø§Ù„Ù†Ø´Ø· ÙÙ‚Ø·."
            : "This assistant is limited to your active branch.";

    // An honest "I don't have that information" for an uncovered domain/entity.
    private static string Unsupported(string locale) =>
        locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase)
            ? "عذراً، لا أملك هذه المعلومة. يمكنك السؤال عن المبيعات أو المخزون أو المنتجات أو الطلبات أو الموردين."
            : "Sorry, I don't have that information. Try asking about sales, inventory, products, orders, or suppliers.";

    // Map a classifier-named domain/entity to the catalog's exact spelling, or null if no
    // tool covers it (case-insensitive). Used to set Unsupported* and to canonicalize keys.
    private string? CanonicalDomain(string? d) =>
        string.IsNullOrWhiteSpace(d) ? null
        : _catalog.All.Select(t => t.Domain)
            .FirstOrDefault(x => string.Equals(x, d, StringComparison.OrdinalIgnoreCase));

    private string? CanonicalEntity(string? e) =>
        string.IsNullOrWhiteSpace(e) ? null
        : _catalog.All.SelectMany(t => t.Entities)
            .FirstOrDefault(x => string.Equals(x, e, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Outcome of attempting to answer via a stored plan.</summary>
internal sealed class PlanExecutionResult
{
    public bool Matched { get; set; }
    public bool HasData { get; set; }

    /// <summary>The shaped payload to phrase (null when <see cref="DirectAnswer"/> is set).</summary>
    public object? Data { get; set; }

    /// <summary>A ready answer (template render or a refusal) to use verbatim.</summary>
    public string? DirectAnswer { get; set; }

    public List<string> ToolsUsed { get; set; } = new();
    public bool IsMixing { get; set; }

    // â”€â”€ No-answer instrumentation (code-set) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>Set when this outcome is a no-answer; the code-determined reason.</summary>
    public Domain.Enums.NoAnswerReason? Reason { get; set; }

    /// <summary>The proof code used to set <see cref="Reason"/>.</summary>
    public string? Evidence { get; set; }

    /// <summary>The classification result, carried even on NotMatched so the caller can
    /// build evidence for a fallback no-answer.</summary>
    public string? ClassifyEvidence { get; set; }

    public static PlanExecutionResult NotMatched(string? classifyEvidence = null) =>
        new() { Matched = false, ClassifyEvidence = classifyEvidence };

    public static PlanExecutionResult Direct(string answer) =>
        new() { Matched = true, HasData = false, DirectAnswer = answer };

    /// <summary>A no-answer the plan engine returns to the user, with its code-set reason.</summary>
    public static PlanExecutionResult NoAnswer(string answer, Domain.Enums.NoAnswerReason reason, string evidence) =>
        new() { Matched = true, HasData = false, DirectAnswer = answer, Reason = reason, Evidence = evidence };
}

