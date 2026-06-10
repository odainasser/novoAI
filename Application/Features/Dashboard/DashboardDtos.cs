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
