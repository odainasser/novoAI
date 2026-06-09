using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class StocktakeEndpoints
{
    public static void MapStocktakeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stocktakes").WithTags("Stocktakes");

        // ===== List =====
        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] string? type,
            [FromQuery] string? status,
            [FromQuery] Guid? warehouseId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                    return Results.Ok(new PaginatedList<StocktakeDto>(new List<StocktakeDto>(), 0, pageNumber ?? 1, pageSize ?? 10));
                warehouseId = scope.PrimaryWarehouseId;
            }

            var result = await service.GetAllAsync(
                pageNumber ?? 1, pageSize ?? 10, search, type, status, warehouseId, fromDate, toDate);
            return Results.Ok(result);
        })
        .WithName("GetAllStocktakes")
        .WithSummary("Get all stocktakes with pagination and filters")
        .Produces<PaginatedList<StocktakeDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        // ===== Get one =====
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var stocktake = await service.GetByIdAsync(id);
            if (stocktake == null) return Results.NotFound();

            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (!scope.WarehouseIds.Contains(stocktake.WarehouseId)) return Results.NotFound();
            }

            return Results.Ok(stocktake);
        })
        .WithName("GetStocktakeById")
        .WithSummary("Get a stocktake with its lines")
        .Produces<StocktakeDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        // ===== Create (Draft) =====
        group.MapPost("/", async (
            [FromBody] CreateStocktakeRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                    return Results.BadRequest(new { error = "The selected branch has no warehouse." });
                // Branch panel always counts the branch's own warehouse.
                request.WarehouseId = scope.PrimaryWarehouseId.Value;
            }

            var created = await service.CreateAsync(request);
            return Results.Created($"/api/stocktakes/{created.Id}", created);
        })
        .WithName("CreateStocktake")
        .WithSummary("Create a stocktake (Draft) and snapshot its in-scope lines")
        .Produces<StocktakeDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<CreateStocktakeRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryWrite));

        // ===== Start counting (generate/refresh lines) =====
        group.MapPost("/{id:guid}/start", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var guard = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (guard is not null) return guard;
            var result = await service.StartAsync(id);
            return Results.Ok(result);
        })
        .WithName("StartStocktake")
        .WithSummary("Begin counting: snapshot current quantities and move to InProgress")
        .Produces<StocktakeDto>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryWrite));

        // ===== Save counts (progressive) =====
        group.MapPut("/{id:guid}/counts", async (
            Guid id,
            [FromBody] SaveStocktakeCountsRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var guard = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (guard is not null) return guard;
            var result = await service.SaveCountsAsync(id, request);
            return Results.Ok(result);
        })
        .WithName("SaveStocktakeCounts")
        .WithSummary("Save counted quantities for lines (StockBalance is not touched)")
        .Produces<StocktakeDto>(StatusCodes.Status200OK)
        .AddEndpointFilter<ValidationFilter<SaveStocktakeCountsRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryWrite));

        // ===== Complete =====
        group.MapPost("/{id:guid}/complete", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var guard = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (guard is not null) return guard;
            var result = await service.CompleteAsync(id);
            return Results.Ok(result);
        })
        .WithName("CompleteStocktake")
        .WithSummary("Finalise counting and flag discrepancies for review")
        .Produces<StocktakeDto>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryWrite));

        // ===== Approve (generate adjustments) =====
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            [FromBody] ApproveStocktakeRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var guard = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (guard is not null) return guard;
            var result = await service.ApproveAsync(id, request);
            return Results.Ok(result);
        })
        .WithName("ApproveStocktake")
        .WithSummary("Assign per-line adjustment types and approve, generating adjustments")
        .Produces<StocktakeDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<ApproveStocktakeRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryApprove));

        // ===== Cancel =====
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IStocktakeService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var guard = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (guard is not null) return guard;
            var result = await service.CancelAsync(id);
            return Results.Ok(result);
        })
        .WithName("CancelStocktake")
        .WithSummary("Abandon a stocktake without touching StockBalance")
        .Produces<StocktakeDto>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryWrite));
    }

    /// <summary>
    /// For branch-scoped calls, validates the caller is assigned to the branch and
    /// that the target stocktake belongs to the branch warehouse. Returns a failure
    /// result to short-circuit the endpoint, or null when the call may proceed.
    /// </summary>
    private static async Task<IResult?> EnsureInScopeAsync(
        Guid id, Guid? branchId, HttpContext httpContext,
        IStocktakeService service, IBranchService branchService, CancellationToken cancellationToken)
    {
        if (!branchId.HasValue) return null;

        var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
        if (scope.FailureResult is not null) return scope.FailureResult;

        var stocktake = await service.GetByIdAsync(id);
        if (stocktake == null) return Results.NotFound();
        if (!scope.WarehouseIds.Contains(stocktake.WarehouseId)) return Results.NotFound();

        return null;
    }
}
