using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Features.Assistant;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Services.Assistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Admin surface for the tool-calling assistant. Lists turns as reviewable plans,
/// lets a reviewer correct a turn's plan and save it as a DRAFT governed plan, and
/// manages the plan library (promote → confirmed, deprecate, edit definition).
/// Confirmed plans are what <see cref="AssistantPlanEngine"/> matches and executes.
/// Internal so it can read the (internal) tool catalog. Nothing is auto-applied.
/// </summary>
internal class AssistantAdminService : IAssistantAdminService
{
    private readonly ApplicationDbContext _context;
    private readonly ToolCatalog _catalog;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssistantAdminService> _logger;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AssistantAdminService(
        ApplicationDbContext context,
        ToolCatalog catalog,
        ICurrentUserService currentUserService,
        ILogger<AssistantAdminService> logger)
    {
        _context = context;
        _catalog = catalog;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // ── Interactions ───────────────────────────────────────────────────

    public async Task<PaginatedList<AssistantInteractionDto>> GetInteractionsAsync(
        int pageNumber, int pageSize, string? search = null,
        bool? unansweredOnly = null, bool? confirmedOnly = null)
    {
        await TryLoadCatalogAsync();   // tool domain/entity enrichment is best-effort

        var query = _context.Set<AssistantInteraction>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(i => i.Question.ToLower().Contains(s));
        }
        if (unansweredOnly == true) query = query.Where(i => !i.Answered);
        if (confirmedOnly == true) query = query.Where(i => i.PlanConfirmed);

        query = query.OrderByDescending(i => i.CreatedAt);

        var count = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedList<AssistantInteractionDto>(items.Select(MapInteraction).ToList(), count, pageNumber, pageSize);
    }

    public async Task<AssistantPlanOptionsDto> GetPlanOptionsAsync()
    {
        await _catalog.EnsureLoadedAsync(CancellationToken.None);

        var tools = _catalog.All
            .OrderBy(t => t.Domain).ThenBy(t => t.Name)
            .Select(t => new AssistantToolInfoDto
            {
                Name = t.Name,
                Description = t.Description,
                Domain = t.Domain,
                Entities = t.Entities.ToList(),
                Parameters = ToolSchema.Params(t.ParametersSchema).Select(p => new ToolParamDto
                {
                    Name = p.Name,
                    Type = p.Type,
                    Enum = p.Enum.ToList(),
                    Description = p.Description,
                    Required = p.Required
                }).ToList()
            })
            .ToList();

        return new AssistantPlanOptionsDto
        {
            Tools = tools,
            Domains = tools.Select(t => t.Domain).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().OrderBy(d => d).ToList(),
            Entities = tools.SelectMany(t => t.Entities).Distinct().OrderBy(e => e).ToList()
        };
    }

    public async Task ConfirmPlanAsync(Guid interactionId, ConfirmAssistantPlanRequest request)
    {
        // Validation below resolves tools by name — the catalog must be loaded.
        await _catalog.EnsureLoadedAsync(CancellationToken.None);

        // interactionId may be Guid.Empty when the plan is created from a no-answer
        // cluster (there is no owning interaction in that case).
        AssistantInteraction? interaction = null;
        if (interactionId != Guid.Empty)
            interaction = await _context.Set<AssistantInteraction>().FindAsync(interactionId)
                ?? throw new KeyNotFoundException($"Assistant interaction {interactionId} not found.");

        var (_, actorName) = await _currentUserService.GetCurrentUserAsync();

        // Completeness gate: a plan can only be confirmed when EVERY parameter of EVERY
        // chosen tool has an explicit source (static/extract/context/omit). This makes
        // "the period filter was silently left at its default" an impossible state.
        ValidateComplete(request);

        var sampleQuestion = Truncate(interaction?.Question ?? request.SampleQuestion ?? string.Empty, 2000);

        // 1) Record the correction on the interaction (if there is one).
        if (interaction is not null)
        {
            interaction.ConfirmedTools = Join(request.Tools.Select(t => t.Name));
            interaction.ConfirmedDomain = Blank(request.Domain);
            interaction.ConfirmedEntities = Join(request.Entities);
            interaction.PlanConfirmed = true;
            interaction.ReviewedAt = DateTime.UtcNow;
            interaction.ReviewedBy = actorName;
        }

        // 2) Upsert a DRAFT governed plan from the corrected plan.
        var match = new PlanMatch
        {
            Domains = string.IsNullOrWhiteSpace(request.Domain) ? new() : new() { request.Domain!.Trim() },
            Action = Blank(request.Action) ?? "list",
            Entity = Blank(request.Entity) ?? request.Entities.FirstOrDefault(),
            SecondaryEntity = Blank(request.SecondaryEntity)
                ?? (request.Entities.Count > 1 ? request.Entities[1] : null)
        };

        if (match.IsUsable)
        {
            // Confirming IS the governance step: upsert a single CONFIRMED plan per
            // match key, which the engine uses immediately on the next matching
            // question. No separate draft/promote step.
            var key = match.Key();
            var definition = BuildDefinition(request.Tools, match);
            var defJson = JsonSerializer.Serialize(definition, Json);

            var plan = await _context.Set<AssistantPlan>().FirstOrDefaultAsync(p => p.MatchKey == key);
            if (plan is null)
            {
                _context.Set<AssistantPlan>().Add(new AssistantPlan
                {
                    Id = Guid.NewGuid(),
                    MatchDomains = NormalizeDomains(match.Domains),
                    Action = (match.Action ?? "list").ToLowerInvariant(),
                    Entity = match.Entity!.Trim(),
                    SecondaryEntity = match.SecondaryEntity?.Trim(),
                    MatchKey = key,
                    DefinitionJson = defJson,
                    SampleQuestion = sampleQuestion,
                    Locale = interaction?.Locale ?? "en",
                    Status = PlanStatus.Confirmed,
                    Version = 1,
                    ConfirmedBy = actorName,
                    ConfirmedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                plan.DefinitionJson = defJson;
                plan.SampleQuestion = sampleQuestion;
                plan.Status = PlanStatus.Confirmed;
                plan.Version += 1;
                plan.ConfirmedBy = actorName;
                plan.ConfirmedAt = DateTime.UtcNow;
                plan.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    // ── Build a starter definition from a corrected plan ──────────────

    private PlanDefinition BuildDefinition(List<PlanToolInput> toolInputs, PlanMatch match)
    {
        var def = new PlanDefinition();
        var perms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var idx = 1;
        foreach (var input in toolInputs.Where(t => !string.IsNullOrWhiteSpace(t.Name)))
        {
            var tool = _catalog.Find(input.Name);
            if (tool is null) continue;

            var planTool = new PlanTool { Id = $"t{idx++}", Name = input.Name };
            foreach (var p in input.Params)
            {
                var source = (p.Source ?? "").Trim().ToLowerInvariant();
                if (source is "" or "omit") continue;   // omit = deliberately use the tool default
                planTool.Params.Add(new PlanParam
                {
                    Name = p.Name,
                    Source = source,
                    Value = source == "static" ? p.Value : null,
                    Placeholder = source == "extract"
                        ? (string.IsNullOrWhiteSpace(p.Placeholder) ? $"{{{p.Name}}}" : p.Placeholder)
                        : null,
                    ContextKey = source == "context" ? p.ContextKey : null
                });
            }
            def.Tools.Add(planTool);
            foreach (var perm in tool.Permissions) perms.Add(perm);
        }
        def.RequiredPermissions = perms.ToList();
        def.Output = new PlanOutput
        {
            RowCap = 25,
            PrecomputeTotals = (match.Action == "count") ? new() { "count" } : new(),
            TemplateId = (match.Action == "count" && def.Tools.Count == 1) ? "count" : null
        };
        return def;
    }

    // The completeness gate: every parameter of every chosen tool must have an explicit
    // source. "omit" counts (a deliberate use-the-default); a missing decision does not.
    private void ValidateComplete(ConfirmAssistantPlanRequest request)
    {
        if (request.Tools.Count == 0)
            throw new PlanIncompleteException("Select at least one tool.");

        foreach (var input in request.Tools)
        {
            var tool = _catalog.Find(input.Name)
                ?? throw new PlanIncompleteException($"Unknown tool '{input.Name}'.");

            var decided = input.Params
                .Where(p => !string.IsNullOrWhiteSpace(p.Source))
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var spec in ToolSchema.Params(tool.ParametersSchema))
                if (!decided.Contains(spec.Name))
                    throw new PlanIncompleteException(
                        $"Tool '{input.Name}': parameter '{spec.Name}' needs a source (static / extract / context / omit).");

            foreach (var p in input.Params)
                if (string.Equals(p.Source, "static", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(p.Value))
                    throw new PlanIncompleteException($"Tool '{input.Name}': parameter '{p.Name}' is static but has no value.");
        }
    }

    // ── Mapping ─────────────────────────────────────────────────────────

    private AssistantInteractionDto MapInteraction(AssistantInteraction i)
    {
        var tools = Split(i.ToolsUsed);
        var domains = new List<string>();
        var entities = new List<string>();
        foreach (var name in tools)
        {
            var tool = _catalog.Find(name);
            if (tool is null) continue;
            if (!string.IsNullOrWhiteSpace(tool.Domain) && !domains.Contains(tool.Domain)) domains.Add(tool.Domain);
            foreach (var e in tool.Entities) if (!entities.Contains(e)) entities.Add(e);
        }

        return new AssistantInteractionDto
        {
            Id = i.Id,
            Question = i.Question,
            Locale = i.Locale,
            Answer = i.Answer,
            Answered = i.Answered,
            IsMixing = i.IsMixing,
            CreatedAt = i.CreatedAt,
            Tools = tools,
            Domains = domains,
            Entities = entities,
            PlanConfirmed = i.PlanConfirmed,
            ConfirmedTools = Split(i.ConfirmedTools),
            ConfirmedDomain = i.ConfirmedDomain,
            ConfirmedEntities = Split(i.ConfirmedEntities),
            ReviewedAt = i.ReviewedAt,
            ReviewedBy = i.ReviewedBy
        };
    }

    public async Task<AssistantInteractionDto?> GetInteractionAsync(Guid id)
    {
        await TryLoadCatalogAsync();   // tool domain/entity enrichment is best-effort

        var i = await _context.Set<AssistantInteraction>().FindAsync(id);
        return i is null ? null : MapInteraction(i);
    }

    // Read paths only enrich rows with tool metadata — don't fail the page when the
    // ByteMart catalog is unreachable.
    private async Task TryLoadCatalogAsync()
    {
        try { await _catalog.EnsureLoadedAsync(CancellationToken.None); }
        catch (Exception ex) { _logger.LogWarning(ex, "Assistant tool catalog unavailable; listing without tool metadata."); }
    }

    // ── No-answer review queue ─────────────────────────────────────────

    public async Task<PaginatedList<NoAnswerClusterDto>> GetNoAnswersAsync(
        int pageNumber, int pageSize, string? reason = null, string? search = null)
    {
        var query = _context.Set<AssistantNoAnswer>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(reason) && Enum.TryParse<NoAnswerReason>(reason, true, out var rf))
            query = query.Where(c => c.Reason == rf || c.ReviewedReason == rf);
        // (Every stored cluster is a genuine failure — empty/permission/branch refusals
        //  are honest answers and were never recorded here, so no default filter is needed.)

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c => c.SampleQuestion.ToLower().Contains(s));
        }

        // Rank in memory: mis-refusals (bugs) above coverage gaps, then by frequency.
        var all = await query.ToListAsync();
        var ordered = all
            .OrderBy(c => SeverityRank(Effective(c)))
            .ThenByDescending(c => c.Frequency)
            .ThenByDescending(c => c.LastSeenAt)
            .ToList();

        var count = ordered.Count;
        var items = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(MapCluster).ToList();
        return new PaginatedList<NoAnswerClusterDto>(items, count, pageNumber, pageSize);
    }

    private static NoAnswerReason Effective(AssistantNoAnswer c) => c.ReviewedReason ?? c.Reason;

    // Backward bugs (missing-detail / operational) before forward coverage gaps.
    private static int SeverityRank(NoAnswerReason r) => r switch
    {
        NoAnswerReason.MissingParameter or NoAnswerReason.Error => 0,
        NoAnswerReason.NoCallingTool or NoAnswerReason.UnsupportedDomain or NoAnswerReason.UnsupportedEntity => 1,
        _ => 2
    };

    private static bool IsMisRefusal(NoAnswerReason r) =>
        r is NoAnswerReason.MissingParameter;
    private static bool IsCoverageGap(NoAnswerReason r) =>
        r is NoAnswerReason.NoCallingTool or NoAnswerReason.UnsupportedDomain or NoAnswerReason.UnsupportedEntity;

    private static NoAnswerClusterDto MapCluster(AssistantNoAnswer c)
    {
        var eff = c.ReviewedReason ?? c.Reason;
        return new NoAnswerClusterDto
        {
            Id = c.Id,
            Reason = c.Reason.ToString(),
            ReviewedReason = c.ReviewedReason?.ToString(),
            EffectiveReason = eff.ToString(),
            SampleQuestion = c.SampleQuestion,
            Locale = c.Locale,
            Evidence = c.Evidence,
            Frequency = c.Frequency,
            UserFacingMessage = c.UserFacingMessage,
            BranchId = c.BranchId,
            SampleInteractionId = c.SampleInteractionId,
            IsMisRefusal = IsMisRefusal(eff),
            IsCoverageGap = IsCoverageGap(eff),
            LastSeenAt = c.LastSeenAt,
            ReviewedAt = c.ReviewedAt,
            ReviewedBy = c.ReviewedBy,
            CreatedAt = c.CreatedAt
        };
    }

    // ── Reported answers review queue ──────────────────────────────────

    public async Task<PaginatedList<ReportedAnswerDto>> GetReportedAnswersAsync(
        int pageNumber, int pageSize, bool? resolved = null, string? search = null)
    {
        var query = _context.Set<AssistantReportedAnswer>().AsQueryable();

        if (resolved.HasValue)
            query = query.Where(r => r.Resolved == resolved.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(r => r.Question.ToLower().Contains(s)
                                  || r.Answer.ToLower().Contains(s)
                                  || (r.Feedback != null && r.Feedback.ToLower().Contains(s)));
        }

        query = query.OrderBy(r => r.Resolved).ThenByDescending(r => r.CreatedAt);

        var count = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedList<ReportedAnswerDto>(items.Select(MapReport).ToList(), count, pageNumber, pageSize);
    }

    public async Task ResolveReportedAnswerAsync(Guid id, bool resolved)
    {
        var report = await _context.Set<AssistantReportedAnswer>().FindAsync(id)
            ?? throw new KeyNotFoundException($"Reported answer {id} not found.");

        var (_, actorName) = await _currentUserService.GetCurrentUserAsync();

        report.Resolved = resolved;
        report.ReviewedAt = resolved ? DateTime.UtcNow : null;
        report.ReviewedBy = resolved ? actorName : null;
        report.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    private static ReportedAnswerDto MapReport(AssistantReportedAnswer r) => new()
    {
        Id = r.Id,
        Question = r.Question,
        Answer = r.Answer,
        Feedback = r.Feedback,
        Locale = r.Locale,
        BranchId = r.BranchId,
        ReportedBy = r.ReportedBy,
        Resolved = r.Resolved,
        ReviewedAt = r.ReviewedAt,
        ReviewedBy = r.ReviewedBy,
        CreatedAt = r.CreatedAt
    };

    // ── Small helpers ───────────────────────────────────────────────────

    private static string NormalizeDomains(IEnumerable<string> domains) =>
        string.Join(",", domains.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).Distinct());

    private static List<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string? Join(IEnumerable<string>? values)
    {
        var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToList();
        return list is null || list.Count == 0 ? null : string.Join(",", list);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
