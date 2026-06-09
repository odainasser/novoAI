using Api.Authorization;
using Application.Common.Models;
using Application.Features.Warehouses;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class WarehouseEndpoints
{
    public static void MapWarehouseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/warehouses").WithTags("Warehouses");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromQuery] Guid? warehouseTypeId,
            [FromQuery] Guid? branchId,
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var result = await warehouseService.GetAllWarehousesAsync(
                pageNumber ?? 1, pageSize ?? 10, search, isActive, warehouseTypeId, branchId);
            return Results.Ok(result);
        })
        .WithName("GetAllWarehouses")
        .WithSummary("Get all warehouses with pagination")
        .Produces<PaginatedList<WarehouseDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesRead));

        group.MapGet("/active", async (
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var result = await warehouseService.GetActiveWarehousesAsync();
            return Results.Ok(result);
        })
        .WithName("GetActiveWarehouses")
        .WithSummary("Get active warehouses")
        .Produces<List<WarehouseDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var warehouse = await warehouseService.GetWarehouseByIdAsync(id);
            return warehouse == null ? Results.NotFound() : Results.Ok(warehouse);
        })
        .WithName("GetWarehouseById")
        .WithSummary("Get warehouse by ID")
        .Produces<WarehouseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesRead));

        group.MapGet("/exists", async (
            [FromQuery] string nameEn,
            [FromQuery] string nameAr,
            [FromQuery] Guid? excludeWarehouseId,
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var exists = await warehouseService.CheckWarehouseExistsAsync(nameEn, nameAr, excludeWarehouseId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckWarehouseExists")
        .WithSummary("Check if a warehouse with the given names exists")
        .Produces<object>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesRead));

        group.MapGet("/central-exists", async (
            [FromQuery] Guid? excludeWarehouseId,
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var exists = await warehouseService.CheckCentralWarehouseExistsAsync(excludeWarehouseId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckCentralWarehouseExists")
        .WithSummary("Check if a central warehouse already exists")
        .Produces<object>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesRead));

        group.MapPost("/", async (
            [FromBody] CreateWarehouseRequest request,
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var created = await warehouseService.CreateWarehouseAsync(request);
            return Results.Created($"/api/warehouses/{created.Id}", created);
        })
        .WithName("CreateWarehouse")
        .WithSummary("Create a new warehouse")
        .Produces<WarehouseDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateWarehouseRequest request,
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            var updated = await warehouseService.UpdateWarehouseAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateWarehouse")
        .WithSummary("Update an existing warehouse")
        .Produces<WarehouseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IWarehouseService warehouseService,
            CancellationToken cancellationToken) =>
        {
            await warehouseService.DeleteWarehouseAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteWarehouse")
        .WithSummary("Delete a warehouse")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.WarehousesDelete));
    }
}
