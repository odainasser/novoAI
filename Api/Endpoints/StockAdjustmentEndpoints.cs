using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class StockAdjustmentEndpoints
{
    public static void MapStockAdjustmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock-adjustments").WithTags("StockAdjustments");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] Guid? warehouseId,
            [FromQuery] string? adjustmentType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStockAdjustmentService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                {
                    return Results.Ok(new PaginatedList<StockAdjustmentDto>(new List<StockAdjustmentDto>(), 0, pageNumber ?? 1, pageSize ?? 10));
                }
                warehouseId = scope.PrimaryWarehouseId;
            }

            var result = await service.GetAllAsync(
                pageNumber ?? 1, pageSize ?? 10, search, status, warehouseId, adjustmentType, fromDate, toDate);
            return Results.Ok(result);
        })
        .WithName("GetAllStockAdjustments")
        .WithSummary("Get all stock adjustments with pagination and filters")
        .Produces<PaginatedList<StockAdjustmentDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStockAdjustmentService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var adjustment = await service.GetByIdAsync(id);
            if (adjustment == null) return Results.NotFound();

            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (!scope.WarehouseIds.Contains(adjustment.WarehouseId)) return Results.NotFound();
            }

            return Results.Ok(adjustment);
        })
        .WithName("GetStockAdjustmentById")
        .WithSummary("Get stock adjustment by ID")
        .Produces<StockAdjustmentDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapPost("/", async (
            [FromBody] CreateStockAdjustmentRequest request,
            [FromServices] IStockAdjustmentService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateAsync(request);
            return Results.Created($"/api/stock-adjustments/{created.Id}", created);
        })
        .WithName("CreateStockAdjustment")
        .WithSummary("Create a new stock adjustment and apply immediately")
        .Produces<StockAdjustmentDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<CreateStockAdjustmentRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IStockAdjustmentService service,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteStockAdjustment")
        .WithSummary("Delete a stock adjustment")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryDelete));
    }
}
