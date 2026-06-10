namespace Web.Models.Dashboard;

public class DashboardSummaryDto
{
    // Apps integration module
    public int ActiveApps { get; set; }
    public int TotalApps { get; set; }

    // Assistant question volume & quality
    public int TotalQuestions { get; set; }
    public int QuestionsToday { get; set; }
    public int AnsweredQuestions { get; set; }
    public int AnsweredRatePercent { get; set; }

    // Governance & review queues
    public int ConfirmedPlans { get; set; }
    public int OpenNoAnswers { get; set; }
    public int UnresolvedReports { get; set; }

    // Identity (secondary)
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }

    public List<RecentQuestionDto> RecentQuestions { get; set; } = new();
}

public class RecentQuestionDto
{
    public Guid Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? AppName { get; set; }
    public bool Answered { get; set; }
    public DateTime CreatedAt { get; set; }
}
