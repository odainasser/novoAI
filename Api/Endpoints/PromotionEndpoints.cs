using Api.Authorization;
using Api.Filters;
using Application.Common.Models;
using Application.Features.Promotions;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class PromotionEndpoints
{
    public static void MapPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/promotions").WithTags("Promotions");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] bool? isActive,
            [FromServices] IPromotionService promotionService,
            CancellationToken cancellationToken) =>
        {
            var promotions = await promotionService.GetAllPromotionsAsync(
                pageNumber ?? 1,
                pageSize ?? 10,
                search,
                isActive);
            return Results.Ok(promotions);
        })
        .WithName("GetAllPromotions")
        .WithSummary("Get all promotions with pagination and filters - Requires promotions.read permission")
        .Produces<PaginatedList<PromotionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PromotionsRead));

        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IPromotionService promotionService,
            CancellationToken cancellationToken) =>
        {
            var promotion = await promotionService.GetPromotionByIdAsync(id);
            return promotion == null ? Results.NotFound() : Results.Ok(promotion);
        })
        .WithName("GetPromotionById")
        .WithSummary("Get promotion by ID - Requires promotions.read permission")
        .Produces<PromotionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PromotionsRead));

        group.MapGet("/active", async (
            [FromServices] IPromotionService promotionService,
            CancellationToken cancellationToken) =>
        {
            var promotions = await promotionService.GetActivePromotionsAsync();
            return Results.Ok(promotions);
        })
        .WithName("GetActivePromotions")
        .WithSummary("Get all currently active promotions - Requires authorization")
        .Produces<List<PromotionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization();

        group.MapGet("/unit/{unitId:guid}", async (
            Guid unitId,
            [FromServices] IPromotionService promotionService,
            CancellationToken cancellationToken) =>
        {
            var promotions = await promotionService.GetPromotionsForUnitAsync(unitId);
            return Results.Ok(promotions);
        })
        .WithName("GetPromotionsForUnit")
        .WithSummary("Get all active promotions applicable to a selling unit - Requires promotions.read permission")
        .Produces<List<PromotionDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PromotionsRead));

        group.MapPost("/", async (
            [FromBody] CreatePromotionRequest request,
            [FromServices] IPromotionService promotionService,
            CancellationToken cancellationToken) =>
        {
            var created = await promotionService.CreatePromotionAsync(request);
            return Results.Created($"/api/promotions/{created.Id}", created);
        })
        .WithName("CreatePromotion")
        .WithSummary("Create a new promotion - Requires promotions.write permission")
        .Produces<PromotionDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PromotionsWrite))
        .AddEndpointFilter<ValidationFilter<CreatePromotionRequest>>();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdatePromotionRequest request,
            [FromServices] IPromotionService promotionService,
            CancellationToken cancellationToken) =>
        {
            var updated = await promotionService.UpdatePromotionAsync(id, request);
            return Results.Ok(updated);
        })
        .WithName("UpdatePromotion")
        .WithSummary("Update an existing promotion - Requires promotions.write permission")
        .Produces<PromotionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PromotionsWrite))
        .AddEndpointFilter<ValidationFilter<UpdatePromotionRequest>>();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromServices] IPromotionService promotionService,
            CancellationToken cancellationToken) =>
        {
            await promotionService.DeletePromotionAsync(id);
            return Results.NoContent();
        })
        .WithName("DeletePromotion")
        .WithSummary("Delete a promotion - Requires promotions.delete permission")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.PromotionsDelete));
    }
}
