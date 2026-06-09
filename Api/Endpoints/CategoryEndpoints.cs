using Api.Authorization;
using Application.Common.Models;
using Application.Features.Categories;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories").WithTags("Categories");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] Guid? parentId,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var categories = await categoryService.GetAllCategoriesAsync(pageNumber ?? 1, pageSize ?? 10, parentId, search, isActive);
            return Results.Ok(categories);
        })
        .WithName("GetAllCategories")
        .WithSummary("Get all categories with pagination - Requires categories.read permission")
        .Produces<PaginatedList<CategoryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesRead));

        group.MapGet("/root", async (
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var categories = await categoryService.GetRootCategoriesAsync();
            return Results.Ok(categories);
        })
        .WithName("GetRootCategories")
        .WithSummary("Get root categories (no parent) - Requires categories.read permission")
        .Produces<List<CategoryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesRead));

        group.MapGet("/tree", async (
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var tree = await categoryService.GetCategoryTreeAsync();
            return Results.Ok(tree);
        })
        .WithName("GetCategoryTree")
        .WithSummary("Get category tree structure - Requires categories.read permission")
        .Produces<List<CategoryTreeDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var category = await categoryService.GetCategoryByIdAsync(id);
            return category == null ? Results.NotFound() : Results.Ok(category);
        })
        .WithName("GetCategoryById")
        .WithSummary("Get category by ID - Requires categories.read permission")
        .Produces<CategoryDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesRead));

        group.MapGet("/exists", async (
            [FromQuery] string nameEn,
            [FromQuery] string nameAr,
            [FromQuery] Guid? excludeCategoryId,
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var exists = await categoryService.CheckCategoryExistsAsync(nameEn, nameAr, excludeCategoryId);
            return Results.Ok(new { exists });
        })
        .WithName("CheckCategoryExists")
        .WithSummary("Check if a category with the given names exists - Requires categories.read permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesRead));

        group.MapPost("/", async (
            [FromBody] CreateCategoryRequest request,
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var created = await categoryService.CreateCategoryAsync(request);
            return Results.Created($"/api/categories/{created.Id}", created);
        })
        .WithName("CreateCategory")
        .WithSummary("Create a new category - Requires categories.write permission")
        .Produces<CategoryDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesWrite));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateCategoryRequest request,
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            var updated = await categoryService.UpdateCategoryAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdateCategory")
        .WithSummary("Update an existing category - Requires categories.write permission")
        .Produces<CategoryDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesWrite));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] ICategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            await categoryService.DeleteCategoryAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteCategory")
        .WithSummary("Delete a category - Requires categories.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesDelete));

        group.MapPost("/{id:guid}/image", async (
            Guid id,
            IFormFile file,
            [FromServices] ICategoryService categoryService,
            [FromServices] IMediaService mediaService,
            CancellationToken cancellationToken) =>
        {
            // Verify category exists
            var category = await categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return Results.NotFound();
            }

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded.");
            }

            // Delete existing image if any
            var existingMedia = await mediaService.GetMediaForEntityAsync(id, EntityType.Category, "image");
            foreach (var media in existingMedia)
            {
                await mediaService.DeleteMediaAsync(media.Id);
            }

            // Upload new image
            using var stream = file.OpenReadStream();
            var newMedia = await mediaService.UploadMediaAsync(id, EntityType.Category, stream, file.FileName, file.ContentType, "image");

            return Results.Ok(new { id = newMedia.Id, url = mediaService.GetMediaUrl(newMedia) });
        })
        .WithName("UploadCategoryImage")
        .WithSummary("Upload an image for a category - Requires categories.write permission")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .DisableAntiforgery()
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesWrite));

        group.MapDelete("/{id:guid}/image", async (
            Guid id,
            [FromServices] ICategoryService categoryService,
            [FromServices] IMediaService mediaService,
            CancellationToken cancellationToken) =>
        {
            // Verify category exists
            var category = await categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return Results.NotFound();
            }

            // Delete existing image
            var existingMedia = await mediaService.GetMediaForEntityAsync(id, EntityType.Category, "image");
            foreach (var media in existingMedia)
            {
                await mediaService.DeleteMediaAsync(media.Id);
            }

            return Results.NoContent();
        })
        .WithName("RemoveCategoryImage")
        .WithSummary("Remove the image from a category - Requires categories.write permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.CategoriesWrite));
    }
}
