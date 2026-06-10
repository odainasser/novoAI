using Application.Features.Dashboard;
using Application.Services;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Aggregates the dashboard summary: the Apps integration module, assistant
/// question volume/quality, the governed plan library, the review queues, and a
/// short recent-questions feed.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;

        var totalApps = await _context.Apps.CountAsync();
        var activeApps = await _context.Apps.CountAsync(a => a.IsActive);

        var totalQuestions = await _context.AssistantInteractions.CountAsync();
        var questionsToday = await _context.AssistantInteractions.CountAsync(i => i.CreatedAt >= today);
        var answeredQuestions = await _context.AssistantInteractions.CountAsync(i => i.Answered);

        var confirmedPlans = await _context.AssistantPlans.CountAsync(p => p.Status == PlanStatus.Confirmed);
        var openNoAnswers = await _context.AssistantNoAnswers.CountAsync();
        var unresolvedReports = await _context.AssistantReportedAnswers.CountAsync(r => !r.Resolved);

        // Answer quality: good turns / all turns. Failed turns (the no-answer queue,
        // cluster frequencies summed) and user-reported answers both count against it.
        var failedTurns = await _context.AssistantNoAnswers.SumAsync(c => (int?)c.Frequency) ?? 0;
        var reportedTotal = await _context.AssistantReportedAnswers.CountAsync();
        var allTurns = totalQuestions + failedTurns;
        var goodTurns = Math.Max(0, totalQuestions - reportedTotal);
        var qualityPercent = allTurns == 0 ? 0 : (int)Math.Round(goodTurns * 100.0 / allTurns);

        // Recent questions with their app name (subquery left-join — legacy rows keep working).
        var recentQuestions = await _context.AssistantInteractions.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .Take(8)
            .Select(i => new RecentQuestionDto
            {
                Id = i.Id,
                Question = i.Question,
                Answered = i.Answered,
                CreatedAt = i.CreatedAt,
                AppName = _context.Apps.IgnoreQueryFilters()
                    .Where(a => a.Id == i.AppId)
                    .Select(a => a.Name)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return new DashboardSummaryDto
        {
            ActiveApps = activeApps,
            TotalApps = totalApps,
            TotalQuestions = totalQuestions,
            QuestionsToday = questionsToday,
            AnsweredQuestions = answeredQuestions,
            AnsweredRatePercent = totalQuestions == 0
                ? 0
                : (int)Math.Round(answeredQuestions * 100.0 / totalQuestions),
            QualityPercent = qualityPercent,
            FailedTurns = failedTurns,
            ReportedTotal = reportedTotal,
            ConfirmedPlans = confirmedPlans,
            OpenNoAnswers = openNoAnswers,
            UnresolvedReports = unresolvedReports,
            RecentQuestions = recentQuestions
        };
    }
}
