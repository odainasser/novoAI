using Application.Services;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class MediaEndpoints
{
    // Security: Maximum file size (10 MB) - also enforced in MediaService
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media").WithTags("Media");

        group.MapPost("/{entityType}/{entityId}", async (
            string entityType,
            Guid entityId,
            IFormFile file,
            [FromQuery] string? collectionName,
            [FromServices] IMediaService mediaService) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            // Security: Check file size at endpoint level for early rejection
            if (file.Length > MaxFileSizeBytes)
                return Results.BadRequest($"File size exceeds maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.");

            if (!Enum.TryParse<EntityType>(entityType, true, out var parsedEntityType))
                return Results.BadRequest($"Invalid entity type: {entityType}");

            try
            {
                using var stream = file.OpenReadStream();
                var media = await mediaService.UploadMediaAsync(
                    entityId,
                    parsedEntityType,
                    stream,
                    file.FileName,
                    file.ContentType,
                    collectionName ?? "default");

                return Results.Ok(media);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .WithName("UploadMedia")
        .WithSummary("Upload media for an entity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .DisableAntiforgery()
        .RequireAuthorization();

        group.MapGet("/{entityType}/{entityId}", async (
            string entityType,
            Guid entityId,
            [FromQuery] string? collectionName,
            [FromServices] IMediaService mediaService) =>
        {
            if (!Enum.TryParse<EntityType>(entityType, true, out var parsedEntityType))
                return Results.BadRequest($"Invalid entity type: {entityType}");

            var mediaList = await mediaService.GetMediaForEntityAsync(entityId, parsedEntityType, collectionName);
            return Results.Ok(mediaList);
        })
        .WithName("GetMediaForEntity")
        .WithSummary("Get media for an entity")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IMediaService mediaService) =>
        {
            await mediaService.DeleteMediaAsync(id);
            return Results.NoContent();
        })
        .WithName("DeleteMedia")
        .WithSummary("Delete a media item")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();

        group.MapPut("/{id:guid}/set-main", async (
            Guid id,
            [FromQuery] Guid entityId,
            [FromQuery] string entityType,
            [FromQuery] string collectionName,
            [FromServices] IMediaService mediaService) =>
        {
            if (!Enum.TryParse<EntityType>(entityType, true, out var parsedEntityType))
                return Results.BadRequest($"Invalid entity type: {entityType}");

            await mediaService.SetMainMediaAsync(id, entityId, parsedEntityType, collectionName);
            return Results.NoContent();
        })
        .WithName("SetMainMedia")
        .WithSummary("Set a media item as main")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
    }
}
