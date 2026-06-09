using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Units;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class UnitEndpoints
{
    public static void MapUnitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/units").WithTags("Units");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] Guid? productId,
            [FromQuery] Guid? unitOfMeasureId,
            [FromQuery] bool? isActive,
            [FromQuery] Guid? unitTypeId,
            [FromQuery] Guid? categoryId,
            [FromQuery] Guid? supplierId,
            [FromQuery] Domain.Enums.ItemStatus? status,
            [FromServices] IUnitService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetAllAsync(pageNumber ?? 1, pageSize ?? 10, search, productId, unitOfMeasureId, isActive, unitTypeId, categoryId, supplierId, status);
            return Results.Ok(result);
        })
        .WithName("GetAllUnits")
        .WithSummary("Get all units with pagination and filters - Requires units.read permission")
        .Produces<PaginatedList<UnitDto>>(StatusCodes.Status200OK)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IUnitService service,
            CancellationToken cancellationToken) =>
        {
            var item = await service.GetByIdAsync(id);
            return item == null ? Results.NotFound() : Results.Ok(item);
        })
        .WithName("GetUnitById")
        .WithSummary("Get unit by ID - Requires units.read permission")
        .Produces<UnitDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsRead));

        group.MapPost("/", async (
            [FromBody] CreateUnitRequest request,
            [FromServices] IUnitService service,
            CancellationToken cancellationToken) =>
        {
            var created = await service.CreateAsync(request);
            return Results.Created($"/api/units/{created.Id}", created);
        })
        .WithName("CreateUnit")
        .WithSummary("Create a new unit - Requires units.write permission")
        .Produces<UnitDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsWrite))
        .AddEndpointFilter<ValidationFilter<CreateUnitRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateUnitRequest request,
            [FromServices] IUnitService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.UpdateAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateUnit")
        .WithSummary("Update a unit - Requires units.write permission")
        .Produces<UnitDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsWrite))
        .AddEndpointFilter<ValidationFilter<UpdateUnitRequest>>();

        group.MapPut("/{id:guid}/selling-details", async (
            Guid id,
            [FromBody] SetSellingDetailsRequest request,
            [FromServices] IUnitService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.SetSellingDetailsAsync(id, request.SellingPrice, request.SellingBarcode, request.LowStockThreshold);
            return Results.Ok(updated);
        })
        .WithName("SetSellingDetails")
        .WithSummary("Set selling details (price & barcode) - Requires units.price permission")
        .Produces<UnitDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsPrice));

        group.MapPut("/{id:guid}/logistics-details", async (
            Guid id,
            [FromBody] SetLogisticsDetailsRequest request,
            [FromServices] IUnitService service,
            CancellationToken cancellationToken) =>
        {
            var updated = await service.SetLogisticsDetailsAsync(id, request.Cost, request.Suppliers, request.LowStockThreshold);
            return Results.Ok(updated);
        })
        .WithName("SetLogisticsDetails")
        .WithSummary("Set logistics details (cost & suppliers) - Requires units.logistics permission")
        .Produces<UnitDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsLogistics));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IUnitService service,
            CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteUnit")
        .WithSummary("Delete a unit - Requires units.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.UnitsDelete));
    }
}
