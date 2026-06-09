using Api.Authorization;
using Application.Common.Models;
using Application.Features.Suppliers;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/suppliers").WithTags("Suppliers");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromServices] ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            var suppliers = await supplierService.GetAllSuppliersAsync(
                pageNumber ?? 1,
                pageSize ?? 10,
                search,
                isActive);
            return Results.Ok(suppliers);
        })
        .WithName("GetAllSuppliers")
        .WithSummary("Get all suppliers with pagination and filters - Requires suppliers.read permission")
        .Produces<PaginatedList<SupplierDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SuppliersRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            var supplier = await supplierService.GetSupplierByIdAsync(id);
            return supplier == null ? Results.NotFound() : Results.Ok(supplier);
        })
        .WithName("GetSupplierById")
        .WithSummary("Get supplier by ID - Requires suppliers.read permission")
        .Produces<SupplierDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SuppliersRead));

        group.MapGet("/exists", async (
            [FromQuery] string nameEn,
            [FromQuery] string nameAr,
            [FromQuery] Guid? excludeSupplierId,
            [FromServices] ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            var exists = await supplierService.CheckSupplierExistsAsync(nameEn, nameAr, excludeSupplierId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckSupplierExists")
        .WithSummary("Check if supplier name exists - Requires suppliers.read permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SuppliersRead));

        group.MapGet("/email-exists", async (
            [FromQuery] string email,
            [FromQuery] Guid? excludeSupplierId,
            [FromServices] ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            var exists = await supplierService.CheckSupplierEmailExistsAsync(email, excludeSupplierId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckSupplierEmailExists")
        .WithSummary("Check if supplier email exists - Requires suppliers.read permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SuppliersRead));

        group.MapPost("/", async (
            [FromBody] CreateSupplierRequest request,
            [FromServices] ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            var created = await supplierService.CreateSupplierAsync(request);
            return Results.Created($"/api/suppliers/{created.Id}", created);
        })
        .WithName("CreateSupplier")
        .WithSummary("Create a new supplier - Requires suppliers.write permission")
        .Produces<SupplierDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SuppliersWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSupplierRequest request,
            [FromServices] ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var updated = await supplierService.UpdateSupplierAsync(id, request);
                return Results.Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("UpdateSupplier")
        .WithSummary("Update an existing supplier - Requires suppliers.write permission")
        .Produces<SupplierDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SuppliersWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ISupplierService supplierService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await supplierService.DeleteSupplierAsync(id);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .WithName("DeleteSupplier")
        .WithSummary("Delete a supplier - Requires suppliers.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SuppliersDelete));
    }
}
