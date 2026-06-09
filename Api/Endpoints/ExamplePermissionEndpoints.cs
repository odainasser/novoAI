using Api.Authorization;
using Application.Common.Models;
using Application.Features.Users;
using Application.Services;
using Domain.Constants;

namespace Api.Endpoints;

public static class ExamplePermissionEndpoints
{
    /// <summary>
    /// Example endpoints showing how to use permission-based authorization
    /// </summary>
    public static void MapExamplePermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/examples")
            .WithTags("Examples - Permission Based Authorization");

        // Example 1: Single permission requirement
        group.MapGet("/users-readonly", async (IUserService userService) =>
        {
            var users = await userService.GetAllUsersAsync(1, 10);
            return Results.Ok(users);
        })
        .WithName("GetUsersReadOnly")
        .WithSummary("Get all users - Requires users.read permission")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersRead))
        .Produces<PaginatedList<UserResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Example 2: Multiple permission requirements (using policy check in code)
        group.MapPost("/users-create", async (CreateUserRequest request, IUserService userService) =>
        {
            // Permission check is done by the authorization attribute
            var user = await userService.CreateUserAsync(request);
            return Results.Created($"/api/users/{user.Id}", user);
        })
        .WithName("CreateUserExample")
        .WithSummary("Create user - Requires users.write permission")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersWrite))
        .Produces<UserResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // Example 3: Endpoint accessible to all authenticated users (no specific permission)
        group.MapGet("/current-user-permissions", (HttpContext context) =>
        {
            var permissions = context.User.FindAll("permission")
                .Select(c => c.Value)
                .ToList();

            var roles = context.User.FindAll(System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            return Results.Ok(new
            {
                UserId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                Email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                Roles = roles,
                Permissions = permissions
            });
        })
        .WithName("GetCurrentUserPermissions")
        .WithSummary("Get current user's roles and permissions")
        .RequireAuthorization()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // Example 4: Admin-only endpoint (requires system.audit permission)
        group.MapGet("/admin-settings", () =>
        {
            return Results.Ok(new
            {
                Message = "This endpoint requires admin permissions",
                Settings = new { /* admin settings */ }
            });
        })
        .WithName("GetAdminSettings")
        .WithSummary("Get admin settings - Requires system.audit permission")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SystemAudit))
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
