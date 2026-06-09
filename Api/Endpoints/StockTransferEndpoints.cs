using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class StockTransferEndpoints
{
    public static void MapStockTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock-transfers").WithTags("StockTransfers");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] Guid? warehouseId,
            [FromQuery] string? transferType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStockTransferService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                {
                    return Results.Ok(new PaginatedList<StockTransferDto>(new List<StockTransferDto>(), 0, pageNumber ?? 1, pageSize ?? 10));
                }
                warehouseId = scope.PrimaryWarehouseId;
            }

            var result = await service.GetAllAsync(
                pageNumber ?? 1, pageSize ?? 10, search, warehouseId, transferType, fromDate, toDate);
            return Results.Ok(result);
        })
        .WithName("GetAllStockTransfers")
        .WithSummary("Get all stock transfers with pagination and filters")
        .Produces<PaginatedList<StockTransferDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStockTransferService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var transfer = await service.GetByIdAsync(id);
            if (transfer == null) return Results.NotFound();

            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                // A transfer is "in" the branch if either end of the move belongs to it.
                if (!scope.WarehouseIds.Contains(transfer.FromWarehouseId) &&
                    !scope.WarehouseIds.Contains(transfer.ToWarehouseId))
                {
                    return Results.NotFound();
                }
            }

            return Results.Ok(transfer);
        })
        .WithName("GetStockTransferById")
        .WithSummary("Get stock transfer by ID")
        .Produces<StockTransferDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapPost("/", async (
            [FromBody] CreateStockTransferRequest request,
            [FromServices] IStockTransferService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateAsync(request);
            return Results.Created($"/api/stock-transfers/{created.Id}", created);
        })
        .WithName("CreateStockTransfer")
        .WithSummary("Create a new stock transfer between warehouses")
        .Produces<StockTransferDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<CreateStockTransferRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IStockTransferService service,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteStockTransfer")
        .WithSummary("Delete a stock transfer")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryDelete));
    }
}
