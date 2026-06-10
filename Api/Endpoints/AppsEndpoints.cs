using Api.Authorization;
using Application.Features.Apps;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

/// <summary>
/// The Apps integration module: client systems integrate WITH ByteAI by being
/// registered here. An app row (code + tool-provider base URL + persona) is the
/// whole onboarding step — its tool catalog is discovered live from the base URL.
/// </summary>
public static class AppsEndpoints
{
    public static void MapAppsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/apps").WithTags("Apps");

        group.MapGet("/", async (
            [FromServices] IAppsAdminService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAppsAsync(cancellationToken)))
        .WithName("GetApps")
        .WithSummary("List the registered client applications")
        .Produces<IReadOnlyList<AppDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AppsRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IAppsAdminService service,
            CancellationToken cancellationToken) =>
        {
            var dto = await service.GetAppAsync(id, cancellationToken);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        })
        .WithName("GetApp")
        .Produces<AppDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AppsRead));

        group.MapPost("/", async (
            [FromBody] SaveAppRequest request,
            [FromServices] IAppsAdminService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var dto = await service.CreateAppAsync(request, cancellationToken);
                return Results.Created($"/api/apps/{dto.Id}", dto);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateApp")
        .WithSummary("Register a new client application")
        .Produces<AppDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AppsWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] SaveAppRequest request,
            [FromServices] IAppsAdminService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await service.UpdateAppAsync(id, request, cancellationToken);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        })
        .WithName("UpdateApp")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AppsWrite));

        group.MapPost("/{id:guid}/active", async (
            Guid id,
            [FromQuery] bool isActive,
            [FromServices] IAppsAdminService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await service.SetActiveAsync(id, isActive, cancellationToken);
                return Results.NoContent();
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
        })
        .WithName("SetAppActive")
        .WithSummary("Activate or deactivate a registered application")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.AppsWrite));
    }
}
