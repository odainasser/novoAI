using System.Text.RegularExpressions;
using Application.Features.Assistant;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// Logs every assistant turn for the admin "plan" page and supplies recent
/// reviewer-confirmed plans back to the model as few-shot supervision. Best-effort:
/// any failure here is swallowed and never breaks answering. Nothing is auto-applied.
/// </summary>
public class AssistantLearningService : IAssistantLearningService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AssistantLearningService> _logger;

    public AssistantLearningService(ApplicationDbContext context, ILogger<AssistantLearningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex Quoted = new(@"['""][^'""]+['""]", RegexOptions.Compiled);
    private static readonly Regex Numbers = new(@"\d+", RegexOptions.Compiled);

    public async Task<Guid> RecordInteractionAsync(
        string question,
        string locale,
        IReadOnlyList<string> toolsUsed,
        bool answered,
        bool isMixing,
        string answer,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = Guid.NewGuid();
            _context.Set<AssistantInteraction>().Add(new AssistantInteraction
            {
                Id = id,
                Question = Truncate(question, 2000),
                Locale = locale,
                ToolsUsed = toolsUsed.Count == 0 ? null : string.Join(",", toolsUsed),
                Answered = answered,
                IsMixing = isMixing,
                Answer = Truncate(answer, 2000),
                BranchId = branchId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record assistant interaction for: {Question}", question);
            return Guid.Empty;
        }
    }

    public async Task RecordNoAnswerAsync(
        string question,
        string locale,
        NoAnswerReason reason,
        string evidence,
        Guid? branchId,
        string? userFacingMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = Normalize(question);
            var clusterKey = $"{(int)reason}|{normalized}";

            var cluster = await _context.Set<AssistantNoAnswer>()
                .FirstOrDefaultAsync(c => c.ClusterKey == clusterKey, cancellationToken);

            if (cluster is null)
            {
                _context.Set<AssistantNoAnswer>().Add(new AssistantNoAnswer
                {
                    Id = Guid.NewGuid(),
                    Reason = reason,
                    NormalizedQuestion = normalized,
                    ClusterKey = clusterKey,
                    SampleQuestion = Truncate(question, 2000),
                    Locale = locale,
                    Evidence = Truncate(evidence, 2000),
                    BranchId = branchId,
                    Frequency = 1,
                    UserFacingMessage = Truncate(userFacingMessage ?? "", 1000),
                    LastSeenAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                cluster.Frequency++;
                cluster.LastSeenAt = DateTime.UtcNow;
                cluster.SampleQuestion = Truncate(question, 2000);
                cluster.Evidence = Truncate(evidence, 2000);
                cluster.UserFacingMessage = Truncate(userFacingMessage ?? "", 1000);
                // Leave human triage decisions intact across recurrences.
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record no-answer for: {Question}", question);
        }
    }

    public async Task RecordReportedAnswerAsync(
        string question,
        string answer,
        string? feedback,
        string locale,
        Guid? branchId,
        string? reportedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Set<AssistantReportedAnswer>().Add(new AssistantReportedAnswer
            {
                Id = Guid.NewGuid(),
                Question = Truncate(question, 2000),
                Answer = Truncate(answer, 8000),
                Feedback = string.IsNullOrWhiteSpace(feedback) ? null : Truncate(feedback.Trim(), 2000),
                Locale = string.IsNullOrWhiteSpace(locale) ? "en" : locale,
                BranchId = branchId,
                ReportedBy = reportedBy,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record reported answer for: {Question}", question);
        }
    }

    // Cluster phrasings: lower-case, collapse whitespace, and placeholder quoted names
    // and numbers so "supplier 'Acme' aging" and "orders over 500" group with variants.
    private static string Normalize(string question)
    {
        var n = question.Trim().ToLowerInvariant();
        n = Quoted.Replace(n, "{x}");
        n = Numbers.Replace(n, "{n}");
        n = Whitespace.Replace(n, " ");
        return Truncate(n, 400);
    }

    public async Task<IReadOnlyList<ConfirmedPlanExample>> GetConfirmedPlanExamplesAsync(
        int max, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _context.Set<AssistantInteraction>()
                .AsNoTracking()
                .Where(i => i.PlanConfirmed && i.ConfirmedTools != null && i.ConfirmedTools != "")
                .OrderByDescending(i => i.ReviewedAt)
                .Take(Math.Clamp(max, 1, 20))
                .Select(i => new { i.Question, i.ConfirmedTools })
                .ToListAsync(cancellationToken);

            return rows
                .Select(r => new ConfirmedPlanExample(
                    r.Question,
                    (r.ConfirmedTools ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load confirmed plan examples; continuing without few-shot.");
            return Array.Empty<ConfirmedPlanExample>();
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
