using Api.Authorization;
using Application.Common.Models;
using Application.Features.Lookups;
using Application.Services;
using Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lookups").WithTags("Lookups");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? parentCode,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromServices] ILookupService lookupService,
            CancellationToken cancellationToken) =>
        {
            var lookups = await lookupService.GetAllLookupsAsync(pageNumber ?? 1, pageSize ?? 10, parentCode, search, isActive);
            return Results.Ok(lookups);
        })
        .WithName("GetAllLookups")
        .WithSummary("Get all lookups - Requires lookups.read permission")
        .Produces<PaginatedList<LookupDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsRead));

        group.MapGet("/byparent", async (
            [FromQuery] string parentCode,
            [FromServices] ILookupService lookupService,
            CancellationToken cancellationToken) =>
        {
            var items = await lookupService.GetLookupsByParentAsync(parentCode);
            return Results.Ok(items);
        })
        .WithName("GetLookupsByParent")
        .WithSummary("Get lookups by parent code - Requires lookups.read permission")
        .Produces<List<LookupDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsRead));

        group.MapGet("/roots", async (
            [FromServices] ILookupService lookupService,
            CancellationToken cancellationToken) =>
        {
            var items = await lookupService.GetRootLookupsAsync();
            return Results.Ok(items);
        })
        .WithName("GetRootLookups")
        .WithSummary("Get root lookups (no parent) - Requires lookups.read permission")
        .Produces<List<LookupDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ILookupService lookupService,
            CancellationToken cancellationToken) =>
        {
            var lookup = await lookupService.GetLookupByIdAsync(id);
            return lookup == null ? Results.NotFound() : Results.Ok(lookup);
        })
        .WithName("GetLookupById")
        .WithSummary("Get lookup by ID - Requires lookups.read permission")
        .Produces<LookupDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsRead));

        group.MapGet("/exists", async (
            [FromQuery] string code,
            [FromQuery] string nameEn,
            [FromQuery] string nameAr,
            [FromQuery] Guid? excludeLookupId,
            [FromServices] ILookupService lookupService,
            CancellationToken cancellationToken) =>
        {
            var (codeExists, nameEnExists, nameArExists) = await lookupService.CheckLookupExistsAsync(code, nameEn, nameAr, excludeLookupId);
            return Results.Ok(new { codeExists, nameEnExists, nameArExists });
        })
        .WithName("CheckLookupExists")
        .WithSummary("Check if a lookup code, nameEn, or nameAr already exists - Requires lookups.read permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsRead));

        group.MapPost("/", async (
            [FromBody] CreateLookupRequest request,
            [FromServices] ILookupService lookupService,
            [FromServices] IValidator<CreateLookupRequest>? validator,
            CancellationToken cancellationToken) =>
        {
            if (validator != null)
            {
                var validationResult = await validator.ValidateAsync(request, cancellationToken);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                        .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());

                    return Results.ValidationProblem(errors, title: "Validation Failed", detail: "One or more validation errors occurred.");
                }
            }

            var created = await lookupService.CreateLookupAsync(request);
            return Results.Created($"/api/lookups/{created.Id}", created);
        })
        .WithName("CreateLookup")
        .WithSummary("Create a new lookup - Requires lookups.write permission")
        .Produces<LookupDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateLookupRequest request,
            [FromServices] ILookupService lookupService,
            [FromServices] IValidator<UpdateLookupRequest>? validator,
            CancellationToken cancellationToken) =>
        {
            if (validator != null)
            {
                var validationResult = await validator.ValidateAsync(request, cancellationToken);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                        .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());

                    return Results.ValidationProblem(errors, title: "Validation Failed", detail: "One or more validation errors occurred.");
                }
            }

            var updated = await lookupService.UpdateLookupAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateLookup")
        .WithSummary("Update an existing lookup - Requires lookups.write permission")
        .Produces<LookupDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ILookupService lookupService,
            CancellationToken cancellationToken) =>
        {
            await lookupService.DeleteLookupAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteLookup")
        .WithSummary("Delete a lookup - Requires lookups.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.LookupsDelete));
    }
}
