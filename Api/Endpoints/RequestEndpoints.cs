using Api.Authorization;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Features.Requests;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class RequestEndpoints
{
    public static void MapRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/requests").WithTags("Requests");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] RequestType? type,
            [FromQuery] RequestStatus? status,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IRequestService requestService,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            // Branch-scoped: return only requests submitted by users in the branch.
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;

                var userIds = await branchService.GetUserIdsForBranchAsync(branchId.Value, cancellationToken);
                var byUser = await requestService.GetByRequesterIdsAsync(userIds, pageNumber ?? 1, pageSize ?? 10, status);
                return Results.Ok(byUser);
            }

            var result = await requestService.GetAllRequestsAsync(
                pageNumber ?? 1,
                pageSize ?? 10,
                search,
                type,
                status);
            return Results.Ok(result);
        })
        .WithName("GetAllRequests")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.GetRequestByIdAsync(id);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetRequestById")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsRead));

        group.MapGet("/pending-product-update/{productId:guid}", async (
            Guid productId,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.GetPendingProductUpdateRequestAsync(productId);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetPendingProductUpdateRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsRead));

        group.MapPost("/set-unit-price", async (
            [FromBody] CreateSetUnitPriceRequest request,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.CreateSetUnitPriceRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateSetUnitPriceRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPost("/activate-product", async (
            [FromBody] CreateActivateProductRequest request,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.CreateActivateProductRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateActivateProductRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPost("/activate-unit", async (
            [FromBody] CreateActivateUnitRequest request,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.CreateActivateUnitRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateActivateUnitRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPut("/{id:guid}/review", async (
            Guid id,
            [FromBody] ReviewRequestDto review,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.ReviewRequestAsync(id, review);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("ReviewRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPost("/add-grn", async (
            [FromBody] CreateInventoryGRNRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IRequestService requestService,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            // GRN destination is resolved server-side in GoodsReceivingService.CreateAsync
            // (always Central). branchId is used only for membership scoping when a branch
            // user submits the request.
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
            }

            var result = await requestService.CreateAddGRNRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateAddGRNRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPut("/{id:guid}/add-grn", async (
            Guid id,
            [FromBody] CreateInventoryGRNRequest request,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.UpdateAddGRNRequestAsync(id, request);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("UpdateAddGRNRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPost("/add-stock-adjustment", async (
            [FromBody] CreateInventoryAdjustmentRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IRequestService requestService,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            // When called by a branch user with branchId, the WarehouseId in the
            // request body is overridden with the branch's MW warehouse so a
            // forged value can't escape the scope.
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null) return Results.BadRequest(new { error = "Branch warehouse not configured." });

                request.Data ??= new CreateStockAdjustmentRequest();
                request.Data.WarehouseId = scope.PrimaryWarehouseId.Value;
            }

            var result = await requestService.CreateAddStockAdjustmentRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateAddStockAdjustmentRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPost("/add-stock-transfer", async (
            [FromBody] CreateInventoryTransferRequest request,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IRequestService requestService,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null) return Results.BadRequest(new { error = "Branch warehouse not configured." });

                request.Data ??= new CreateStockTransferRequest();
                request.Data.WarehouseId = scope.PrimaryWarehouseId.Value;
            }

            var result = await requestService.CreateAddStockTransferRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateAddStockTransferRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapPost("/delete-product", async (
            [FromBody] CreateDeleteProductRequest request,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.CreateDeleteProductRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateDeleteProductRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsDelete));

        group.MapPost("/delete-unit", async (
            [FromBody] CreateDeleteUnitRequest request,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.CreateDeleteUnitRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateDeleteUnitRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsDelete));

        group.MapPost("/set-logistics-details", async (
            [FromBody] CreateSetLogisticsDetailsRequest request,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.CreateSetLogisticsDetailsRequestAsync(request);
            return Results.Created($"/api/requests/{result.Id}", result);
        })
        .WithName("CreateSetLogisticsDetailsRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapGet("/pending-logistics/{unitId:guid}", async (
            Guid unitId,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.GetPendingSetLogisticsDetailsRequestAsync(unitId);
            return result == null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetPendingSetLogisticsDetailsRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsRead));

        group.MapGet("/my", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] RequestType? type,
            [FromQuery] RequestStatus? status,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.GetMyRequestsAsync(
                pageNumber ?? 1,
                pageSize ?? 10,
                search,
                type,
                status);
            return Results.Ok(result);
        })
        .WithName("GetMyRequests")
        .RequireAuthorization();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.DeleteRequestAsync(id);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteRequest")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapDelete("/my/{id:guid}", async (
            Guid id,
            [FromServices] IRequestService requestService) =>
        {
            var result = await requestService.DeleteMyRequestAsync(id);
            return result ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteMyRequest")
        .RequireAuthorization();
    }
}
