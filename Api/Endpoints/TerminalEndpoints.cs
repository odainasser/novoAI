using Api.Authorization;
using Application.Common.Models;
using Application.Features.Terminals;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class TerminalEndpoints
{
    public static void MapTerminalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/terminals").WithTags("Terminals");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromQuery] Guid? branchId,
            [FromServices] ITerminalService terminalService,
            CancellationToken cancellationToken) =>
        {
            var result = await terminalService.GetAllTerminalsAsync(
                pageNumber ?? 1, pageSize ?? 10, search, isActive, branchId);
            return Results.Ok(result);
        })
        .WithName("GetAllTerminals")
        .WithSummary("Get all terminals with pagination")
        .Produces<PaginatedList<TerminalDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.TerminalsRead));

        group.MapGet("/active", async (
            [FromServices] ITerminalService terminalService,
            CancellationToken cancellationToken) =>
        {
            var result = await terminalService.GetActiveTerminalsAsync();
            return Results.Ok(result);
        })
        .WithName("GetActiveTerminals")
        .WithSummary("Get active terminals")
        .Produces<List<TerminalDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.TerminalsRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ITerminalService terminalService,
            CancellationToken cancellationToken) =>
        {
            var terminal = await terminalService.GetTerminalByIdAsync(id);
            return terminal == null ? Results.NotFound() : Results.Ok(terminal);
        })
        .WithName("GetTerminalById")
        .WithSummary("Get terminal by ID")
        .Produces<TerminalDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.TerminalsRead));

        group.MapGet("/exists", async (
            [FromQuery] string nameEn,
            [FromQuery] string nameAr,
            [FromQuery] Guid? excludeTerminalId,
            [FromServices] ITerminalService terminalService,
            CancellationToken cancellationToken) =>
        {
            var exists = await terminalService.CheckTerminalExistsAsync(nameEn, nameAr, excludeTerminalId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckTerminalExists")
        .WithSummary("Check if a terminal with the given names exists")
        .Produces<object>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.TerminalsRead));

        group.MapPost("/", async (
            [FromBody] CreateTerminalRequest request,
            [FromServices] ITerminalService terminalService,
            CancellationToken cancellationToken) =>
        {
            var created = await terminalService.CreateTerminalAsync(request);
            return Results.Created($"/api/terminals/{created.Id}", created);
        })
        .WithName("CreateTerminal")
        .WithSummary("Create a new terminal")
        .Produces<TerminalDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.TerminalsWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateTerminalRequest request,
            [FromServices] ITerminalService terminalService,
            CancellationToken cancellationToken) =>
        {
            var updated = await terminalService.UpdateTerminalAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateTerminal")
        .WithSummary("Update an existing terminal")
        .Produces<TerminalDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.TerminalsWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ITerminalService terminalService,
            CancellationToken cancellationToken) =>
        {
            await terminalService.DeleteTerminalAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteTerminal")
        .WithSummary("Delete a terminal")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.TerminalsDelete));
    }
}
