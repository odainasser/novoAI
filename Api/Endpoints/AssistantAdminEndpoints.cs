using Api.Authorization;
using Application.Common.Models;
using Application.Features.Assistant;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

/// <summary>
/// Admin surface for the tool-calling assistant's single "plan" page: list the
/// turns (each enriched with its executed plan), expose the code-owned plan
/// vocabulary for the correction dropdowns, and confirm a corrected plan.
/// Reuses the existing assistant admin permissions.
/// </summary>
public static class AssistantAdminEndpoints
{
    public static void MapAssistantAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assistant-admin").WithTags("AssistantAdmin");

        group.MapGet("/interactions", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] bool? unansweredOnly,
            [FromQuery] bool? confirmedOnly,
            [FromServices] IAssistantAdminService service) =>
        {
            var result = await service.GetInteractionsAsync(pageNumber ?? 1, pageSize ?? 20, search, unansweredOnly, confirmedOnly);
            return Results.Ok(result);
        })
        .WithName("GetAssistantInteractions")
        .Produces<PaginatedList<AssistantInteractionDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AssistantKeywordsRead));

        group.MapGet("/plan-options", async (
            [FromQuery] Guid? appId,
            [FromServices] IAssistantAdminService service) =>
            Results.Ok(await service.GetPlanOptionsAsync(appId)))
        .WithName("GetAssistantPlanOptions")
        .Produces<AssistantPlanOptionsDto>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AssistantKeywordsRead));

        group.MapPost("/interactions/{id:guid}/confirm-plan", async (
            Guid id,
            [FromBody] ConfirmAssistantPlanRequest request,
            [FromServices] IAssistantAdminService service) =>
        {
            try
            {
                await service.ConfirmPlanAsync(id, request);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (PlanIncompleteException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ConfirmAssistantPlan")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AssistantKeywordsApprove));

        group.MapGet("/interactions/{id:guid}", async (Guid id, [FromServices] IAssistantAdminService service) =>
        {
            var i = await service.GetInteractionAsync(id);
            return i is null ? Results.NotFound() : Results.Ok(i);
        })
        .WithName("GetAssistantInteraction")
        .Produces<AssistantInteractionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AssistantKeywordsRead));

        // ── No-answer review queue ───────────────────────────────────────
        group.MapGet("/no-answers", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? reason,
            [FromQuery] string? search,
            [FromServices] IAssistantAdminService service) =>
            Results.Ok(await service.GetNoAnswersAsync(pageNumber ?? 1, pageSize ?? 20, reason, search)))
        .WithName("GetAssistantNoAnswers")
        .Produces<PaginatedList<NoAnswerClusterDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AssistantKeywordsRead));

        // ── Reported answers review queue ────────────────────────────────
        group.MapGet("/reported", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] bool? resolved,
            [FromQuery] string? search,
            [FromServices] IAssistantAdminService service) =>
            Results.Ok(await service.GetReportedAnswersAsync(pageNumber ?? 1, pageSize ?? 20, resolved, search)))
        .WithName("GetAssistantReportedAnswers")
        .Produces<PaginatedList<ReportedAnswerDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AssistantKeywordsRead));

        group.MapPost("/reported/{id:guid}/resolve", async (
            Guid id,
            [FromQuery] bool? resolved,
            [FromServices] IAssistantAdminService service) =>
        {
            try
            {
                await service.ResolveReportedAnswerAsync(id, resolved ?? true);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("ResolveAssistantReportedAnswer")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AssistantKeywordsWrite));
    }
}
