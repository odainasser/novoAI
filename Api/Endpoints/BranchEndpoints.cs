using Api.Authorization;
using Application.Common.Models;
using Application.Features.Branches;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Endpoints;

public static class BranchEndpoints
{
    public static void MapBranchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/branches").WithTags("Branches");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var branches = await branchService.GetAllBranchesAsync(pageNumber ?? 1, pageSize ?? 10, search, isActive);
            return Results.Ok(branches);
        })
        .WithName("GetAllBranches")
        .WithSummary("Get all branches with pagination - Requires branches.read permission")
        .Produces<PaginatedList<BranchDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesRead));

        group.MapGet("/active", async (
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var branches = await branchService.GetActiveBranchesAsync();
            return Results.Ok(branches);
        })
        .WithName("GetActiveBranches")
        .WithSummary("Get active branches - Requires branches.read permission")
        .Produces<List<BranchDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesRead));

        // Branch Panel: branches assigned to the currently authenticated user via UserBranch.
        // Authenticated only — no extra permission, since every employee reads their own assignments.
        group.MapGet("/mine", async (
            HttpContext httpContext,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Results.Unauthorized();
            }

            var branches = await branchService.GetBranchesAssignedToUserAsync(userId);
            return Results.Ok(branches);
        })
        .WithName("GetMyBranches")
        .WithSummary("Get branches assigned to the current user (Branch Panel selector)")
        .Produces<List<BranchDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        // Resolves the branch's primary back-office (MW) warehouse. Used by the
        // Branch Panel write forms (GRN / Adjustment / Transfer) so they can
        // populate WarehouseId before submitting. Caller must either hold
        // branches.read OR be a member of the branch.
        group.MapGet("/{branchId:guid}/warehouse", async (
            Guid branchId,
            HttpContext httpContext,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId, cancellationToken);
            if (scope.FailureResult is not null) return scope.FailureResult;

            var warehouse = await branchService.GetBranchWarehouseAsync(branchId, cancellationToken);
            return warehouse is null ? Results.NotFound() : Results.Ok(warehouse);
        })
        .WithName("GetBranchWarehouse")
        .WithSummary("Resolves the branch's primary back-office warehouse (MW)")
        .Produces<BranchWarehouseInfo>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var branch = await branchService.GetBranchByIdAsync(id);
            return branch == null ? Results.NotFound() : Results.Ok(branch);
        })
        .WithName("GetBranchById")
        .WithSummary("Get branch by ID - Requires branches.read permission")
        .Produces<BranchDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesRead));

        group.MapGet("/exists", async (
            [FromQuery] string nameEn,
            [FromQuery] string nameAr,
            [FromQuery] Guid? excludeBranchId,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var exists = await branchService.CheckBranchExistsAsync(nameEn, nameAr, excludeBranchId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckBranchExists")
        .WithSummary("Check if a branch with the given names exists - Requires branches.read permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesRead));

        group.MapPost("/", async (
            [FromBody] CreateBranchRequest request,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var created = await branchService.CreateBranchAsync(request);
            return Results.Created($"/api/branches/{created.Id}", created);
        })
        .WithName("CreateBranch")
        .WithSummary("Create a new branch - Requires branches.write permission")
        .Produces<BranchDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateBranchRequest request,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var updated = await branchService.UpdateBranchAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateBranch")
        .WithSummary("Update an existing branch - Requires branches.write permission")
        .Produces<BranchDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            await branchService.DeleteBranchAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteBranch")
        .WithSummary("Delete a branch - Requires branches.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesDelete));

        group.MapPost("/{id:guid}/image", async (
            Guid id,
            IFormFile file,
            [FromServices] IBranchService branchService,
            [FromServices] IMediaService mediaService,
            CancellationToken cancellationToken) =>
        {
            // Verify branch exists
            var branch = await branchService.GetBranchByIdAsync(id);
            if (branch == null)
            {
                return Results.NotFound();
            }

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded.");
            }

            // Delete existing image if any
            var existingMedia = await mediaService.GetMediaForEntityAsync(id, EntityType.Branch, "image");
            foreach (var media in existingMedia)
            {
                await mediaService.DeleteMediaAsync(media.Id);
            }

            // Upload new image
            using var stream = file.OpenReadStream();
            var newMedia = await mediaService.UploadMediaAsync(id, EntityType.Branch, stream, file.FileName, file.ContentType, "image");

            return Results.Ok(new { id = newMedia.Id, url = mediaService.GetMediaUrl(newMedia) });
        })
        .WithName("UploadBranchImage")
        .WithSummary("Upload an image for a branch - Requires branches.write permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .DisableAntiforgery()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesWrite));

        group.MapDelete("/{id:guid}/image", async (
            Guid id,
            [FromServices] IBranchService branchService,
            [FromServices] IMediaService mediaService,
            CancellationToken cancellationToken) =>
        {
            // Verify branch exists
            var branch = await branchService.GetBranchByIdAsync(id);
            if (branch == null)
            {
                return Results.NotFound();
            }

            // Delete existing image
            var existingMedia = await mediaService.GetMediaForEntityAsync(id, EntityType.Branch, "image");
            foreach (var media in existingMedia)
            {
                await mediaService.DeleteMediaAsync(media.Id);
            }

            return Results.NoContent();
        })
        .WithName("RemoveBranchImage")
        .WithSummary("Remove the image from a branch - Requires branches.write permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.BranchesWrite));
    }
}
