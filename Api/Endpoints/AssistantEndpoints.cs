using System.Collections.Concurrent;
using System.Security.Claims;
using Application.Services;
using Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints;

public static class AssistantEndpoints
{
    private static readonly ConcurrentDictionary<string, DateTime> RateLimits = new();

    public static void MapAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assistant").WithTags("Assistant");

        group.MapPost("/ask", async (
            [FromBody] AssistantRequest request,
            HttpContext httpContext,
            [FromServices] IAssistantService service,
            [FromServices] IOptions<OllamaSettings> options,
            CancellationToken cancellationToken) =>
        {
            if (!options.Value.Enabled)
                return Results.StatusCode(503);

            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

            if (RateLimits.TryGetValue(userId, out var lastRequest)
                && DateTime.UtcNow - lastRequest < TimeSpan.FromSeconds(3))
            {
                return Results.StatusCode(429);
            }
            RateLimits[userId] = DateTime.UtcNow;

            var permissions = httpContext.User
                .FindAll("permission")
                .Select(c => c.Value)
                .ToList();

            var result = await service.AskAsync(request, userId, permissions, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AskAssistant")
        .WithSummary("Ask the AI assistant a question about business data")
        .Produces<AssistantResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .RequireAuthorization()
        // Per-user window (see Program.cs "assistant" policy) in addition to the
        // 3-second debounce above.
        .RequireRateLimiting("assistant");

        // Report an answer — stores a snapshot (question + answer + optional feedback)
        // for admin review. Any authenticated user of the assistant can report.
        group.MapPost("/report", async (
            [FromBody] AssistantReportRequest request,
            HttpContext httpContext,
            [FromServices] IAssistantService service,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            await service.ReportAnswerAsync(request, userId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("ReportAssistantAnswer")
        .WithSummary("Report an assistant answer for admin review")
        .Produces(StatusCodes.Status204NoContent)
        .RequireAuthorization();
    }
}
