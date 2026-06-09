using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.PurchaseRequests;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class PurchaseRequestEndpoints
{
    public static void MapPurchaseRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/purchase-requests").WithTags("PurchaseRequests");

        // ===== List =====
        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] PurchaseRequestStatus? status,
            [FromQuery] PurchaseRequestSupplySource? supplySource,
            [FromQuery] Guid? warehouseId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IPurchaseRequestService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.WarehouseIds.Count == 0)
                    return Results.Ok(new PaginatedList<PurchaseRequestDto>(new List<PurchaseRequestDto>(), 0, pageNumber ?? 1, pageSize ?? 10));

                var scoped = await service.GetByWarehouseIdsAsync(scope.WarehouseIds, pageNumber ?? 1, pageSize ?? 10, status, supplySource);
                return Results.Ok(scoped);
            }

            var result = await service.GetAllAsync(
                pageNumber ?? 1, pageSize ?? 10, search, status, supplySource, warehouseId, fromDate, toDate);
            return Results.Ok(result);
        })
        .WithName("GetAllPurchaseRequests")
        .WithSummary("Get all purchase requests with pagination and filters")
        .Produces<PaginatedList<PurchaseRequestDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsRead));

        // ===== Get one =====
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IPurchaseRequestService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var pr = await service.GetByIdAsync(id);
            if (pr == null) return Results.NotFound();

            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (!scope.WarehouseIds.Contains(pr.RequestingWarehouseId)) return Results.NotFound();
            }

            return Results.Ok(pr);
        })
        .WithName("GetPurchaseRequestById")
        .WithSummary("Get a purchase request by ID")
        .Produces<PurchaseRequestDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsRead));

        // ===== Create (Draft) =====
        group.MapPost("/", async (
            [FromBody] CreatePurchaseRequestRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IPurchaseRequestService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                    return Results.BadRequest(new { error = "BranchWarehouseNotConfigured" });
                request.RequestingWarehouseId = scope.PrimaryWarehouseId.Value;
            }

            var created = await service.CreateAsync(request);
            return Results.Created($"/api/purchase-requests/{created.Id}", created);
        })
        .WithName("CreatePurchaseRequest")
        .WithSummary("Create a new purchase request (Draft)")
        .Produces<PurchaseRequestDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<CreatePurchaseRequestRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsWrite));

        // ===== Generate auto-reorder proposal drafts =====
        group.MapPost("/auto-reorder", async (
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IPurchaseRequestService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                    return Results.BadRequest(new { error = "BranchWarehouseNotConfigured" });
                warehouseId = scope.PrimaryWarehouseId;
            }

            var created = await service.GenerateAutoReorderProposalsAsync(warehouseId, cancellationToken);
            return Results.Ok(new { created });
        })
        .WithName("GenerateAutoReorderProposals")
        .WithSummary("Scan stock and create draft auto-reorder proposals (never auto-submitted)")
        .Produces(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsWrite));

        // ===== Edit a draft =====
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdatePurchaseRequestRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IPurchaseRequestService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var (ok, failure) = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (!ok) return failure!;

            var updated = await service.UpdateAsync(id, request);
            return updated == null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("UpdatePurchaseRequest")
        .WithSummary("Edit a draft purchase request")
        .Produces<PurchaseRequestDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .AddEndpointFilter<ValidationFilter<UpdatePurchaseRequestRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsWrite));

        // ===== Submit for approval =====
        group.MapPost("/{id:guid}/submit", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IPurchaseRequestService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var (ok, failure) = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (!ok) return failure!;

            var result = await service.SubmitAsync(id);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("SubmitPurchaseRequest")
        .WithSummary("Submit a purchase request for approval")
        .Produces<PurchaseRequestDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsWrite));

        // ===== Approve =====
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            [FromBody] ReviewPurchaseRequestRequest? body,
            [FromServices] IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(id, body?.Note);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("ApprovePurchaseRequest")
        .WithSummary("Approve a purchase request (marks it ready to convert)")
        .Produces<PurchaseRequestDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsApprove));

        // ===== Reject =====
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            [FromBody] ReviewPurchaseRequestRequest? body,
            [FromServices] IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(id, body?.Note);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("RejectPurchaseRequest")
        .WithSummary("Reject a purchase request with a reason")
        .Produces<PurchaseRequestDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsApprove));

        // ===== Convert to downstream document =====
        group.MapPost("/{id:guid}/convert", async (
            Guid id,
            [FromServices] IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.ConvertAsync(id);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("ConvertPurchaseRequest")
        .WithSummary("Convert an approved purchase request to a GRN or stock transfer")
        .Produces<PurchaseRequestDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsConvert));

        // ===== Cancel =====
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IPurchaseRequestService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var (ok, failure) = await EnsureInScopeAsync(id, branchId, httpContext, service, branchService, cancellationToken);
            if (!ok) return failure!;

            var result = await service.CancelAsync(id);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("CancelPurchaseRequest")
        .WithSummary("Cancel a draft or submitted purchase request")
        .Produces<PurchaseRequestDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PurchaseRequestsWrite));
    }

    // Helper: when a branchId is supplied, confirm the PR belongs to a warehouse the caller's branch owns.
    // Returns (true, null) when in scope (or no branchId), otherwise (false, failureResult).
    private static async Task<(bool Ok, IResult? Failure)> EnsureInScopeAsync(
        Guid id, Guid? branchId, HttpContext httpContext,
        IPurchaseRequestService service, IBranchService branchService,
        CancellationToken cancellationToken)
    {
        if (!branchId.HasValue) return (true, null);

        var pr = await service.GetByIdAsync(id);
        if (pr == null) return (false, Results.NotFound());

        var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
        if (scope.FailureResult is not null) return (false, scope.FailureResult);
        if (!scope.WarehouseIds.Contains(pr.RequestingWarehouseId)) return (false, Results.NotFound());

        return (true, null);
    }
}
