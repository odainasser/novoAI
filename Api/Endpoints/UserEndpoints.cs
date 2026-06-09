using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Users;
using Application.Services;
using Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? role,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var users = await userService.GetAllUsersAsync(pageNumber ?? 1, pageSize ?? 10, role, search, isActive, cancellationToken);
            return Results.Ok(users);
        })
        .WithName("GetAllUsers")
        .WithSummary("Get all users - Requires users.read permission")
        .Produces<PaginatedList<UserResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersRead));

        group.MapGet("/active", async (
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var users = await userService.GetActiveUsersAsync(cancellationToken);
            return Results.Ok(users);
        })
        .WithName("GetActiveUsers")
        .WithSummary("Get all active users - Requires users.read permission")
        .Produces<IEnumerable<UserResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersRead));

        group.MapGet("/exists", async (
            [FromQuery] string email,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var exists = await userService.ExistsByEmailAsync(email, cancellationToken);
            return Results.Ok(new { exists });
        })
        .WithName("CheckUserExists")
        .WithSummary("Check if user exists by email")
        .Produces(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.GetUserByIdAsync(id, cancellationToken);
            return user == null ? Results.NotFound() : Results.Ok(user);
        })
        .WithName("GetUserById")
        .WithSummary("Get user by ID - Requires users.read permission")
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersRead));

        group.MapPut("/profile", async (
            [FromBody] UpdateUserRequest request,
            HttpContext httpContext,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var userIdString = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await userService.UpdateUserAsync(userId, request, cancellationToken);
            return Results.Ok(user);
        })
        .WithName("UpdateProfile")
        .WithSummary("Update current user profile")
        .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>()
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesValidationProblem()
        .RequireAuthorization();

        group.MapPost("/", async (
            [FromBody] CreateUserRequest request,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.CreateUserAsync(request, cancellationToken);
            return Results.Created($"/api/users/{user.Id}", user);
        })
        .WithName("CreateUser")
        .WithSummary("Create a new user - Requires users.write permission")
        .AddEndpointFilter<ValidationFilter<CreateUserRequest>>()
        .Produces<UserResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateUserRequest request,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.UpdateUserAsync(id, request, cancellationToken);
            return Results.Ok(user);
        })
        .WithName("UpdateUser")
        .WithSummary("Update an existing user - Requires users.write permission")
        .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>()
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            await userService.DeleteUserAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteUser")
        .WithSummary("Delete a user - Requires users.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersDelete));

        group.MapPost("/{id:guid}/roles", async (
            Guid id,
            [FromBody] AssignRolesRequest request,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var user = await userService.AssignRolesToUserAsync(id, request, cancellationToken);
            return Results.Ok(user);
        })
        .WithName("AssignRolesToUser")
        .WithSummary("Assign roles to a user - Requires users.write permission")
        .Produces<UserResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersWrite));

        // Branch assignments — used by the unified user form for users with
        // the BranchManager role. The set is an authoritative replacement
        // (PUT semantics): pass the full target list every time.
        group.MapGet("/{id:guid}/branches", async (
            Guid id,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var branchIds = await userService.GetUserBranchIdsAsync(id, cancellationToken);
            return Results.Ok(branchIds);
        })
        .WithName("GetUserBranches")
        .WithSummary("Get the list of branches assigned to a user")
        .Produces<List<Guid>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersRead));

        group.MapPut("/{id:guid}/branches", async (
            Guid id,
            [FromBody] AssignBranchesRequest request,
            [FromServices] IUserService userService,
            CancellationToken cancellationToken) =>
        {
            await userService.SetUserBranchesAsync(id, request.BranchIds ?? new List<Guid>(), cancellationToken);
            return Results.NoContent();
        })
        .WithName("SetUserBranches")
        .WithSummary("Replace the set of branches assigned to a user")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UsersWrite));
    }
}

public class AssignBranchesRequest
{
    public List<Guid>? BranchIds { get; set; }
}
