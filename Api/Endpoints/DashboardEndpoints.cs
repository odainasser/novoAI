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

        group.MapGet("/warehouse-stats", async (
            [FromServices] IDashboardService service,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetWarehouseProductStatsAsync(fromDate, toDate);
            return Results.Ok(result);
        })
        .WithName("GetWarehouseProductStats")
        .WithSummary("Get product sold/returned quantities per store")
        .Produces<List<WarehouseProductStatsDto>>(StatusCodes.Status200OK)
        .RequireAuthorization();

        group.MapGet("/warehouse-stock", async (
            [FromServices] IDashboardService service,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetWarehouseCurrentStockAsync(fromDate, toDate);
            return Results.Ok(result);
        })
        .WithName("GetWarehouseCurrentStock")
        .WithSummary("Get current stock quantities per warehouse for all products")
        .Produces<List<WarehouseStockDto>>(StatusCodes.Status200OK)
        .RequireAuthorization();

        group.MapGet("/monthly-revenue", async (
            [FromServices] IDashboardService service,
            [FromQuery] int? months,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetMonthlyRevenueAsync(months ?? 12);
            return Results.Ok(result);
        })
        .WithName("GetMonthlyRevenue")
        .WithSummary("Get monthly revenue per store for the last N months")
        .Produces<List<MonthlyRevenueDto>>(StatusCodes.Status200OK)
        .RequireAuthorization();
    }
}
