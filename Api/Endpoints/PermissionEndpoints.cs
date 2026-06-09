using Api.Authorization;
using Application.Features.Roles;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class PermissionEndpoints
{
    public static void MapPermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/permissions")
            .WithTags("Permissions");

        group.MapGet("/", async (
            [FromServices] IPermissionService permissionService,
            CancellationToken cancellationToken) =>
        {
            var permissions = await permissionService.GetAllPermissionsAsync(cancellationToken);
            return Results.Ok(permissions);
        })
        .WithName("GetAllPermissions")
        .WithSummary("Get all available permissions - Requires roles.read permission")
        .Produces<IEnumerable<PermissionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesRead));

        group.MapGet("/role/{roleId:guid}", async (
            Guid roleId,
            [FromServices] IPermissionService permissionService,
            CancellationToken cancellationToken) =>
        {
            var permissions = await permissionService.GetPermissionsByRoleIdAsync(roleId, cancellationToken);
            return Results.Ok(permissions);
        })
        .WithName("GetPermissionsByRoleId")
        .WithSummary("Get permissions assigned to a specific role - Requires roles.read permission")
        .Produces<IEnumerable<PermissionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RolesRead));
    }
}
