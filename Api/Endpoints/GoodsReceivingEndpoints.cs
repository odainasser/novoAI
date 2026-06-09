using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Inventory;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class GoodsReceivingEndpoints
{
    public static void MapGoodsReceivingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/goods-receiving").WithTags("GoodsReceiving");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] Guid? supplierId,
            [FromQuery] Guid? warehouseId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IGoodsReceivingService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            // When branchId is supplied, scope to the branch's primary (MW) warehouse.
            // BranchScoping handles membership; admins bypass.
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (scope.PrimaryWarehouseId is null)
                {
                    return Results.Ok(new PaginatedList<GoodsReceivingNoteDto>(new List<GoodsReceivingNoteDto>(), 0, pageNumber ?? 1, pageSize ?? 10));
                }
                warehouseId = scope.PrimaryWarehouseId;
            }

            var result = await service.GetAllAsync(
                pageNumber ?? 1, pageSize ?? 10, search, supplierId, warehouseId, fromDate, toDate);
            return Results.Ok(result);
        })
        .WithName("GetAllGoodsReceivingNotes")
        .WithSummary("Get all GRNs with pagination and filters")
        .Produces<PaginatedList<GoodsReceivingNoteDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IGoodsReceivingService service,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            var grn = await service.GetByIdAsync(id);
            if (grn == null) return Results.NotFound();

            // Branch-scoped lookup: a forged id from outside the branch returns 404.
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                if (!scope.WarehouseIds.Contains(grn.WarehouseId)) return Results.NotFound();
            }

            return Results.Ok(grn);
        })
        .WithName("GetGoodsReceivingNoteById")
        .WithSummary("Get GRN by ID")
        .Produces<GoodsReceivingNoteDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryRead));

        group.MapPost("/", async (
            [FromBody] CreateGoodsReceivingNoteRequest request,
            [FromServices] IGoodsReceivingService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateAsync(request);
            return Results.Created($"/api/goods-receiving/{created.Id}", created);
        })
        .WithName("CreateGoodsReceivingNote")
        .WithSummary("Create a new GRN and immediately accept it into stock")
        .Produces<GoodsReceivingNoteDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .AddEndpointFilter<ValidationFilter<CreateGoodsReceivingNoteRequest>>()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.RequestsWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IGoodsReceivingService service,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteGoodsReceivingNote")
        .WithSummary("Delete a Draft GRN")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.InventoryDelete));
    }
}
