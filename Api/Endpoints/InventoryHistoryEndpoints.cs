using Api.Authorization;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class InventoryHistoryEndpoints
{
    public static void MapInventoryHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory-history").WithTags("InventoryHistory");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? unitId,
            [FromQuery] string? actionType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? referenceType,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IInventoryHistoryService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            IEnumerable<Guid>? warehouseIds = null;
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                warehouseIds = scope.WarehouseIds;
            }

            var result = await service.GetAllAsync(
                pageNumber ?? 1, pageSize ?? 10, warehouseId, unitId, actionType, fromDate, toDate, referenceType,
                warehouseIds: warehouseIds);
            return Results.Ok(result);
        })
        .WithName("GetAllInventoryHistory")
        .WithSummary("Get all inventory history records with pagination and filters")
        .Produces<PaginatedList<InventoryHistoryDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IInventoryHistoryService service,
            CancellationToken cancellationToken) =>
        {
            var history = await service.GetByIdAsync(id);
            return history == null ? Results.NotFound() : Results.Ok(history);
        })
        .WithName("GetInventoryHistoryById")
        .WithSummary("Get inventory history record by ID")
        .Produces<InventoryHistoryDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapGet("/balances", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] Guid? warehouseId,
            [FromQuery] string? stockStatus,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IInventoryHistoryService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            // Branch-scoped: targets the branch's primary (MW) warehouse.
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                {
                    return Results.Ok(new PaginatedList<StockBalanceDto>(new List<StockBalanceDto>(), 0, pageNumber ?? 1, pageSize ?? 20));
                }
                warehouseId = scope.PrimaryWarehouseId;
            }

            var result = await service.GetAllStockBalancesAsync(
                pageNumber ?? 1, pageSize ?? 20, search, warehouseId, stockStatus);
            return Results.Ok(result);
        })
        .WithName("GetAllStockBalances")
        .WithSummary("Get all stock balances with pagination and filters")
        .Produces<PaginatedList<StockBalanceDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapGet("/balances/total", async (
            [FromQuery] string? search,
            [FromServices] IInventoryHistoryService service,
            CancellationToken cancellationToken) =>
        {
            var total = await service.GetTotalAvailableBySearchAsync(search ?? string.Empty);
            return Results.Ok(new { TotalAvailable = total });
        })
        .WithName("GetTotalAvailableBySearch")
        .WithSummary("Get total available quantity across all warehouses for a product search")
        .Produces(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapGet("/balances/{warehouseId:guid}", async (
            Guid warehouseId,
            [FromQuery] string? search,
            [FromServices] IInventoryHistoryService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetStockBalancesAsync(warehouseId, search);
            return Results.Ok(result);
        })
        .WithName("GetStockBalances")
        .WithSummary("Get stock balances for a warehouse")
        .Produces<List<StockBalanceDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));
    }
}
