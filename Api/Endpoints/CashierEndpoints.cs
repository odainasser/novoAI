using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Cashiers;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Endpoints;

public static class CashierEndpoints
{
    public static void MapCashierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cashiers")
            .WithTags("Cashiers");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromQuery] Guid? warehouseId,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var cashiers = await cashierService.GetAllCashiersAsync(pageNumber ?? 1, pageSize ?? 10, search, isActive, warehouseId, cancellationToken);
            return Results.Ok(cashiers);
        })
        .WithName("GetAllCashiers")
        .WithSummary("Get all cashiers - Requires cashiers.read permission")
        .Produces<PaginatedList<CashierResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CashiersRead));

        group.MapGet("/active", async (
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var cashiers = await cashierService.GetActiveCashiersAsync(cancellationToken);
            return Results.Ok(cashiers);
        })
        .WithName("GetActiveCashiers")
        .WithSummary("Get all active cashiers - Requires cashiers.read permission")
        .Produces<IEnumerable<CashierResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CashiersRead));

        group.MapGet("/exists", async (
            [FromQuery] string email,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var exists = await cashierService.ExistsByEmailAsync(email, cancellationToken);
            return Results.Ok(new { exists });
        })
        .WithName("CheckCashierExists")
        .WithSummary("Check if cashier exists by email")
        .Produces(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CashiersRead));

        // Get current cashier's own profile - no special permission needed
        group.MapGet("/me", async (
            HttpContext httpContext,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var cashier = await cashierService.GetCashierByIdAsync(userId, cancellationToken);
            return cashier == null ? Results.NotFound() : Results.Ok(cashier);
        })
        .WithName("GetCurrentCashierProfile")
        .WithSummary("Get current cashier's own profile")
        .Produces<CashierResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        // Get current cashier's assigned stores
        group.MapGet("/me/stores", async (
            HttpContext httpContext,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            var stores = await cashierService.GetAssignedStoresAsync(userId, cancellationToken);
            return Results.Ok(stores);
        })
        .WithName("GetMyAssignedStores")
        .WithSummary("Get current cashier's assigned stores")
        .Produces<IEnumerable<AssignedWarehouseDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        // Switch current cashier's active store
        group.MapPost("/me/switch-store", async (
            HttpContext httpContext,
            [FromBody] SwitchStoreRequest request,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var sub = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var cashier = await cashierService.SwitchStoreAsync(userId, request.WarehouseId, cancellationToken);
                return Results.Ok(cashier);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("SwitchMyStore")
        .WithSummary("Switch the current cashier's active store (requires no active shift)")
        .Produces<CashierResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var cashier = await cashierService.GetCashierByIdAsync(id, cancellationToken);
            return cashier == null ? Results.NotFound() : Results.Ok(cashier);
        })
        .WithName("GetCashierById")
        .WithSummary("Get cashier by ID - Requires cashiers.read permission")
        .Produces<CashierResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CashiersRead));

        group.MapPost("/", async (
            [FromBody] CreateCashierRequest request,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var cashier = await cashierService.CreateCashierAsync(request, cancellationToken);
            return Results.Created($"/api/cashiers/{cashier.Id}", cashier);
        })
        .WithName("CreateCashier")
        .WithSummary("Create a new cashier - Requires cashiers.write permission")
        .AddEndpointFilter<ValidationFilter<CreateCashierRequest>>()
        .Produces<CashierResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CashiersWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateCashierRequest request,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            var cashier = await cashierService.UpdateCashierAsync(id, request, cancellationToken);
            return Results.Ok(cashier);
        })
        .WithName("UpdateCashier")
        .WithSummary("Update an existing cashier - Requires cashiers.write permission")
        .AddEndpointFilter<ValidationFilter<UpdateCashierRequest>>()
        .Produces<CashierResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesValidationProblem()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CashiersWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICashierService cashierService,
            CancellationToken cancellationToken) =>
        {
            await cashierService.DeleteCashierAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteCashier")
        .WithSummary("Delete a cashier - Requires cashiers.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CashiersDelete));
    }
}

public class SwitchStoreRequest
{
    public Guid WarehouseId { get; set; }
}
