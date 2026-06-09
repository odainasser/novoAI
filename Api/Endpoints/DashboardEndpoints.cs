using Application.Features.Dashboard;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        group.MapGet("/summary", async (
            [FromServices] IDashboardService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetSummaryAsync();
            return Results.Ok(result);
        })
        .WithName("GetDashboardSummary")
        .WithSummary("Get aggregated dashboard statistics")
        .Produces<DashboardSummaryDto>(StatusCodes.Status200OK)
        .RequireAuthorization();
    }
}
