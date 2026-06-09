using Api.Authorization;
using Application.Features.Shifts;
using Application.Services;
using Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Endpoints;

public static class ShiftEndpoints
{
    public static void MapShiftEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/shifts").WithTags("Shifts");

        // Start shift (cashier)
        group.MapPost("/start", async (
            [FromBody] StartShiftRequest request,
            [FromServices] IShiftService shiftService,
            HttpContext httpContext) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userNameClaim = httpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId)) return Results.Unauthorized();

            try
            {
                var shift = await shiftService.StartShiftAsync(userId, userNameClaim, request);
                return Results.Ok(shift);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthorization();

        // End shift (cashier)
        group.MapPost("/{id:guid}/end", async (
            Guid id,
            [FromBody] EndShiftRequest request,
            [FromServices] IShiftService shiftService,
            HttpContext httpContext) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId)) return Results.Unauthorized();

            try
            {
                var shift = await shiftService.GetShiftByIdAsync(id);
                if (shift == null) return Results.NotFound();
                if (shift.CashierId != userId) return Results.Forbid();

                var result = await shiftService.EndShiftAsync(id, request);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthorization();

        // Get my shifts (cashier)
        group.MapGet("/my", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] Guid? warehouseId,
            [FromServices] IShiftService shiftService,
            HttpContext httpContext) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId)) return Results.Unauthorized();

            var shifts = await shiftService.GetShiftsByCashierAsync(userId, pageNumber ?? 1, pageSize ?? 10, warehouseId);
            return Results.Ok(shifts);
        })
        .RequireAuthorization();

        // Admin: list all shifts (read-only)
        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] Guid? cashierId,
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? branchId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            HttpContext httpContext,
            [FromServices] IShiftService shiftService,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            IEnumerable<Guid>? warehouseIds = null;
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                warehouseIds = scope.WarehouseIds;
            }

            var shifts = await shiftService.GetAllShiftsAsync(
                pageNumber ?? 1, pageSize ?? 20, status, search, cashierId, warehouseId,
                warehouseIds: warehouseIds, fromDate: fromDate, toDate: toDate);
            return Results.Ok(shifts);
        })
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Domain.Constants.Permissions.ShiftsRead));

        // Export shifts to xlsx (honors the same filters as the list endpoint, including
        // branch-scoping via branchId resolved to warehouseIds).
        group.MapGet("/export", async (
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] Guid? cashierId,
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? branchId,
            [FromQuery] bool? ar,
            HttpContext httpContext,
            [FromServices] IShiftService shiftService,
            [FromServices] IBranchService branchService,
            CancellationToken cancellationToken) =>
        {
            IEnumerable<Guid>? warehouseIds = null;
            if (branchId.HasValue)
            {
                var scope = await BranchScoping.ScopeAsync(httpContext, branchService, branchId.Value, cancellationToken);
                if (scope.FailureResult is not null) return scope.FailureResult;
                warehouseIds = scope.WarehouseIds;
            }

            var bytes = await shiftService.ExportShiftsToExcelAsync(
                status, search, cashierId, warehouseId,
                isArabic: ar == true,
                warehouseIds: warehouseIds);
            var fileName = $"shifts-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        })
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Domain.Constants.Permissions.ShiftsRead));
    }
}
