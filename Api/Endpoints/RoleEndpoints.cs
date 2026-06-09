using Api.Authorization;
using Application.Common.Models;
using Application.Features.Roles;
using Application.Services;
using Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles")
            .WithTags("Roles");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromServices] IRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var roles = await roleService.GetAllRolesAsync(pageNumber ?? 1, pageSize ?? 10, cancellationToken);
            return Results.Ok(roles);
        })
        .WithName("GetAllRoles")
        .WithSummary("Get all roles - Requires roles.read permission")
        .Produces<PaginatedList<RoleResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var role = await roleService.GetRoleByIdAsync(id, cancellationToken);
            return role == null ? Results.NotFound() : Results.Ok(role);
        })
        .WithName("GetRoleById")
        .WithSummary("Get role by ID with permissions - Requires roles.read permission")
        .Produces<RoleDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesRead));

        group.MapGet("/exists", async (
            [FromQuery] string name,
            [FromQuery] Guid? excludeRoleId,
            [FromServices] IRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var exists = await roleService.CheckRoleNameExistsAsync(name, excludeRoleId, cancellationToken);
            return Results.Ok(new { exists });
        })
        .WithName("CheckRoleNameExists")
        .WithSummary("Check if a role name already exists - Requires roles.read permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesRead));

        group.MapPost("/", async (
            [FromBody] CreateRoleRequest request,
            [FromServices] IRoleService roleService,
            [FromServices] IValidator<CreateRoleRequest> validator,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                    .ToDictionary(
                        failureGroup => failureGroup.Key,
                        failureGroup => failureGroup.ToArray());

                return Results.ValidationProblem(
                    errors,
                    title: "Validation Failed",
                    detail: "One or more validation errors occurred.");
            }

            var role = await roleService.CreateRoleAsync(request, cancellationToken);
            return Results.Created($"/api/roles/{role.Id}", role);
        })
        .WithName("CreateRole")
        .WithSummary("Create a new role - Requires roles.write permission")
        .Produces<RoleResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateRoleRequest request,
            [FromServices] IRoleService roleService,
            [FromServices] IValidator<UpdateRoleRequest> validator,
            CancellationToken cancellationToken) =>
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                    .ToDictionary(
                        failureGroup => failureGroup.Key,
                        failureGroup => failureGroup.ToArray());

                return Results.ValidationProblem(
                    errors,
                    title: "Validation Failed",
                    detail: "One or more validation errors occurred.");
            }

            var role = await roleService.UpdateRoleAsync(id, request, cancellationToken);
            return Results.Ok(role);
        })
        .WithName("UpdateRole")
        .WithSummary("Update an existing role - Requires roles.write permission")
        .Produces<RoleResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            await roleService.DeleteRoleAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteRole")
        .WithSummary("Delete a role - Requires roles.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesDelete));

        group.MapPost("/{id:guid}/permissions", async (
            Guid id,
            [FromBody] AssignPermissionsRequest request,
            [FromServices] IRoleService roleService,
            CancellationToken cancellationToken) =>
        {
            var role = await roleService.AssignPermissionsToRoleAsync(id, request, cancellationToken);
            return Results.Ok(role);
        })
        .WithName("AssignPermissionsToRole")
        .WithSummary("Assign permissions to a role - Requires roles.write permission")
        .Produces<RoleDetailResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesWrite));
    }
}
