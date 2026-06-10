namespace Application.Features.Dashboard;

/// <summary>
/// Aggregated dashboard statistics, focused on the assistant: registered apps,
/// question volume/quality, the governed plan library, and the review queues.
/// </summary>
public class DashboardSummaryDto
{
    // Apps integration module
    public int ActiveApps { get; set; }
    public int TotalApps { get; set; }

    // Assistant question volume & quality
    public int TotalQuestions { get; set; }
    public int QuestionsToday { get; set; }
    public int AnsweredQuestions { get; set; }

    /// <summary>Share of logged turns that produced a grounded answer (0-100).</summary>
    public int AnsweredRatePercent { get; set; }

    /// <summary>
    /// Answer quality (0-100): good turns / all turns, where all turns include the
    /// failed (no-answer) ones and user-reported answers count against quality.
    /// </summary>
    public int QualityPercent { get; set; }

    /// <summary>Turns that ended in the no-answer queue (cluster frequencies summed).</summary>
    public int FailedTurns { get; set; }

    /// <summary>All user-reported answers (resolved or not) — each was a bad answer.</summary>
    public int ReportedTotal { get; set; }

    // Governance & review queues
    public int ConfirmedPlans { get; set; }
    public int OpenNoAnswers { get; set; }
    public int UnresolvedReports { get; set; }

    /// <summary>The latest assistant turns (newest first).</summary>
    public List<RecentQuestionDto> RecentQuestions { get; set; } = new();
}

/// <summary>One recent assistant turn for the dashboard feed.</summary>
public class RecentQuestionDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? AppName { get; set; }
    public bool Answered { get; set; }
    public DateTime CreatedAt { get; set; }
}
