using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Products;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool? isActive,
            [FromQuery] Guid? warehouseId,
            [FromQuery] bool? onlyWithStock,
            [FromQuery] Domain.Enums.ItemStatus? status,
            [FromServices] IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var products = await productService.GetAllProductsAsync(
                pageNumber ?? 1,
                pageSize ?? 10,
                search,
                categoryId,
                isActive,
                warehouseId,
                onlyWithStock,
                status);
            return Results.Ok(products);
        })
        .WithName("GetAllProducts")
        .WithSummary("Get all products with pagination and filters - Requires products.read permission")
        .Produces<PaginatedList<ProductDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var product = await productService.GetProductByIdAsync(id);
            return product == null ? Results.NotFound() : Results.Ok(product);
        })
        .WithName("GetProductById")
        .WithSummary("Get product by ID - Requires products.read permission")
        .Produces<ProductDetailDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsRead));

        group.MapGet("/code/{code}", async (
            string code,
            [FromServices] IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var product = await productService.GetProductByCodeAsync(code);
            return product == null ? Results.NotFound() : Results.Ok(product);
        })
        .WithName("GetProductByCode")
        .WithSummary("Get product by Code - Requires products.read permission")
        .Produces<ProductDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsRead));

        group.MapGet("/code-exists/{code}", async (
            string code,
            [FromQuery] Guid? excludeProductId,
            [FromServices] IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var exists = await productService.CheckCodeExistsAsync(code, excludeProductId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckCodeExists")
        .WithSummary("Check if Code exists - Requires products.read permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsRead));

        group.MapPost("/", async (
            [FromBody] CreateProductRequest request,
            [FromServices] IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var created = await productService.CreateProductAsync(request);
            return Results.Created($"/api/products/{created.Id}", created);
        })
        .WithName("CreateProduct")
        .WithSummary("Create a new product - Requires products.write permission")
        .Produces<ProductDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsWrite))
        .AddEndpointFilter<ValidationFilter<CreateProductRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProductRequest request,
            [FromServices] IProductService productService,
            CancellationToken cancellationToken) =>
        {
            var updated = await productService.UpdateProductAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateProduct")
        .WithSummary("Update an existing product - Requires products.write permission")
        .Produces<ProductDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsWrite))
        .AddEndpointFilter<ValidationFilter<UpdateProductRequest>>();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IProductService productService,
            CancellationToken cancellationToken) =>
        {
            await productService.DeleteProductAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteProduct")
        .WithSummary("Delete a product - Requires products.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsDelete));

        group.MapPost("/{id:guid}/image", async (
            Guid id,
            IFormFile file,
            [FromServices] IProductService productService,
            [FromServices] IMediaService mediaService,
            CancellationToken cancellationToken) =>
        {
            // Verify product exists
            var product = await productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return Results.NotFound();
            }

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded.");
            }

            // Delete existing image if any
            var existingMedia = await mediaService.GetMediaForEntityAsync(id, EntityType.Product, "image");
            foreach (var media in existingMedia)
            {
                await mediaService.DeleteMediaAsync(media.Id);
            }

            // Upload new image
            using var stream = file.OpenReadStream();
            var newMedia = await mediaService.UploadMediaAsync(id, EntityType.Product, stream, file.FileName, file.ContentType, "image");

            return Results.Ok(new { id = newMedia.Id, url = mediaService.GetMediaUrl(newMedia) });
        })
        .WithName("UploadProductImage")
        .WithSummary("Upload an image for a product - Requires products.write permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .DisableAntiforgery()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsWrite));

        group.MapDelete("/{id:guid}/image", async (
            Guid id,
            [FromServices] IProductService productService,
            [FromServices] IMediaService mediaService,
            CancellationToken cancellationToken) =>
        {
            // Verify product exists
            var product = await productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return Results.NotFound();
            }

            // Delete existing image
            var existingMedia = await mediaService.GetMediaForEntityAsync(id, EntityType.Product, "image");
            foreach (var media in existingMedia)
            {
                await mediaService.DeleteMediaAsync(media.Id);
            }

            return Results.NoContent();
        })
        .WithName("RemoveProductImage")
        .WithSummary("Remove the image from a product - Requires products.write permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.ProductsWrite));
    }
}
