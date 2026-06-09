using Api.Authorization;
using Application.Features.UserLogs;
using Application.Services;
using Application.Common.Interfaces;
using Application.Common.Models;
using Domain.Constants;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Common;
using Domain.Entities;

namespace Api.Endpoints;

public static class UserLogEndpoints
{
    public static void MapUserLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/logs").WithTags("Logs");

        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] Guid? userId,
            [FromQuery] string? entityName,
            [FromQuery] string? entityId,
            [FromQuery] string? culture,
            [FromQuery] string? search,
            [FromQuery] string? matchActions,
            [FromQuery] string? matchEntities,
            [FromServices] IUserLogService userLogService) =>
        {
            var logs = await userLogService.GetLogsAsync(pageNumber ?? 1, pageSize ?? 20, userId, entityName, entityId, culture, search, matchActions, matchEntities);
            return Results.Ok(logs);
        })
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.SystemAudit));
    }
}
