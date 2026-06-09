using Api.Authorization;
using Application.Common.Models;
using Application.Features.Orders;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        // Get all orders with pagination + filters. When branchId is supplied,
        // the response is scoped to the warehouses owned by that branch — the
        // caller must either hold branches.read OR be a member of the branch.
        group.MapGet("/", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] OrderStatus? status,
            [FromQuery] OrderChannel? channel,
            [FromQuery] PaymentMethod? paymentMethod,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? cashierId,
            [FromQuery] Guid? branchId,
            HttpContext httpContext,
            [FromServices] IOrderService orderService,
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

            var orders = await orderService.GetAllOrdersAsync(
                pageNumber ?? 1, pageSize ?? 10,
                search, status, channel, paymentMethod,
                fromDate, toDate, warehouseId, cashierId,
                warehouseIds: warehouseIds);
            return Results.Ok(orders);
        })
        .WithName("GetAllOrders")
        .WithSummary("Get all orders with pagination and filters - Requires orders.read permission")
        .Produces<PaginatedList<OrderDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.OrdersRead));

        // Export orders to xlsx. Honors the same filters as the list endpoint, including
        // branch-scoping via branchId (resolved to warehouseIds the same way as the list).
        group.MapGet("/export", async (
            [FromQuery] string? search,
            [FromQuery] OrderStatus? status,
            [FromQuery] OrderChannel? channel,
            [FromQuery] PaymentMethod? paymentMethod,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? warehouseId,
            [FromQuery] Guid? cashierId,
            [FromQuery] Guid? branchId,
            [FromQuery] bool? ar,
            HttpContext httpContext,
            [FromServices] IOrderService orderService,
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

            var bytes = await orderService.ExportOrdersToExcelAsync(
                search, status, channel, paymentMethod, fromDate, toDate, warehouseId, cashierId,
                isArabic: ar == true,
                warehouseIds: warehouseIds);
            var fileName = $"orders-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        })
        .WithName("ExportOrders")
        .WithSummary("Export orders to xlsx with the same filters as the list endpoint")
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.OrdersRead));

        // Get my orders (Cashier own orders)
        group.MapGet("/my-orders", async (
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            [FromQuery] OrderStatus? status,
            [FromQuery] PaymentMethod? paymentMethod,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? warehouseId,
            [FromServices] IOrderService orderService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var orders = await orderService.GetOrdersByCashierAsync(
                userId,
                pageNumber ?? 1,
                pageSize ?? 10,
                search,
                status,
                paymentMethod,
                fromDate,
                toDate,
                warehouseId);
            return Results.Ok(orders);
        })
        .WithName("GetMyOrders")
        .WithSummary("Get current user's orders - Requires orders.read.own permission")
        .Produces<PaginatedList<OrderDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.OrdersReadOwn));

        // Get order by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            [FromServices] IOrderService orderService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var order = await orderService.GetOrderByIdAsync(id);
            if (order == null)
            {
                return Results.NotFound();
            }

            // Check if user has full access or is the cashier who created the order
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var hasFullAccess = httpContext.User.HasClaim("permission", Permissions.OrdersRead);
            
            if (!hasFullAccess && userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
            {
                if (order.CashierId != userId)
                {
                    return Results.Forbid();
                }
            }

            return Results.Ok(order);
        })
        .WithName("GetOrderById")
        .WithSummary("Get order by ID")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization();

        // Partial refund
        group.MapPost("/{id:guid}/partial-refund", async (
            Guid id,
            [FromBody] PartialRefundRequest? request,
            [FromServices] IOrderService orderService,
            [FromServices] IShiftService shiftService,
            [FromServices] ICashierService cashierService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId)) return Results.Unauthorized();

            // load order to check ownership
            var order = await orderService.GetOrderByIdAsync(id);
            if (order == null) return Results.NotFound();

            // Allow if caller has orders.write permission OR is the cashier who created the order
            var hasWritePermission = httpContext.User.HasClaim("permission", Permissions.OrdersWrite);
            if (!hasWritePermission)
            {
                if (!order.CashierId.HasValue || order.CashierId.Value != userId)
                {
                    return Results.Forbid();
                }

                // Check if cashier is allowed to refund
                var cashier = await cashierService.GetCashierByIdAsync(userId, cancellationToken);
                if (cashier != null && !cashier.CanRefund)
                {
                    return Results.Forbid();
                }

                // Ensure cashier has an active shift
                var hasShift = await shiftService.HasActiveShiftAsync(userId);
                if (!hasShift) return Results.BadRequest(new { error = "Shift not started" });
            }

            if (request == null) return Results.BadRequest(new { error = "Invalid request" });

            // If items specified, perform item-level refund
            if (request.Items != null && request.Items.Any())
            {
                var svcItems = request.Items.Select(i => new Application.Services.PartialRefundItem
                {
                    OrderItemId = i.OrderItemId,
                    Quantity = i.Quantity
                }).ToList();

                var svcResult = await orderService.PartialRefundAsync(id, svcItems, request.ClientCreatedAt);
                if (!svcResult) return Results.NotFound();

                var updatedOrder = await orderService.GetOrderByIdAsync(id);
                return Results.Ok(updatedOrder);
            }

            if (request.Amount <= 0) return Results.BadRequest(new { error = "Invalid amount" });

            // Legacy behavior: amount-based refund is converted to proportional item quantities
            // Calculate items to refund proportionally based on amount
            var orderTotal = order.Total;
            if (request.Amount >= orderTotal)
            {
                var svcResult = await orderService.PartialRefundAsync(id, new List<Application.Services.PartialRefundItem>(), request.ClientCreatedAt);
                if (!svcResult) return Results.NotFound();
                var updatedOrder = await orderService.GetOrderByIdAsync(id);
                return Results.Ok(updatedOrder);
            }

            // Simple proportional mapping: not precise but preserves behavior
            var proportion = request.Amount / orderTotal;
            var derivedItems = order.Items.Select(i => new Application.Services.PartialRefundItem
            {
                OrderItemId = i.Id,
                Quantity = Math.Max(1, (int)Math.Round(i.Quantity * proportion))
            }).ToList();

            var svcResult2 = await orderService.PartialRefundAsync(id, derivedItems, request.ClientCreatedAt);
            if (!svcResult2) return Results.NotFound();

            var updatedOrder2 = await orderService.GetOrderByIdAsync(id);
            return Results.Ok(updatedOrder2);
        })
        .WithName("PartialRefund")
        .WithSummary("Partially refund an order - allowed for admins or the cashier who created the order")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization();

        // Get order by number
        group.MapGet("/number/{orderNumber}", async (
            string orderNumber,
            [FromServices] IOrderService orderService,
            CancellationToken cancellationToken) =>
        {
            var order = await orderService.GetOrderByNumberAsync(orderNumber);
            return order == null ? Results.NotFound() : Results.Ok(order);
        })
        .WithName("GetOrderByNumber")
        .WithSummary("Get order by order number")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        // Create order
        group.MapPost("/", async (
            [FromBody] CreateOrderRequest request,
            [FromServices] IOrderService orderService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userNameClaim = httpContext.User.FindFirst(ClaimTypes.Name)?.Value 
                ?? httpContext.User.FindFirst(ClaimTypes.Email)?.Value;

            Guid? cashierId = null;
            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
            {
                cashierId = userId;
            }

            var order = await orderService.CreateOrderAsync(request, cashierId, userNameClaim);
            return Results.Created($"/api/orders/{order.Id}", order);
        })
        .WithName("CreateOrder")
        .WithSummary("Create a new order - Requires orders.write permission")
        .Produces<OrderDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.OrdersWrite));

        // Update order status
        group.MapPut("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateOrderStatusRequest request,
            [FromServices] IOrderService orderService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var order = await orderService.UpdateOrderStatusAsync(id, request);
                return Results.Ok(order);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpdateOrderStatus")
        .WithSummary("Update order status - Requires orders.write permission")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.OrdersWrite));

        // Refund order
        group.MapPost("/{id:guid}/refund", async (
            Guid id,
            [FromBody] RefundOrderRequest? request,
            [FromServices] IOrderService orderService,
            [FromServices] IShiftService shiftService,
            [FromServices] ICashierService cashierService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId)) return Results.Unauthorized();

            // load order to check ownership
            var order = await orderService.GetOrderByIdAsync(id);
            if (order == null) return Results.NotFound();

            // Allow if caller has orders.write permission OR is the cashier who created the order
            var hasWritePermission = httpContext.User.HasClaim("permission", Permissions.OrdersWrite);
            if (!hasWritePermission)
            {
                if (!order.CashierId.HasValue || order.CashierId.Value != userId)
                {
                    return Results.Forbid();
                }

                // Check if cashier is allowed to refund
                var cashier = await cashierService.GetCashierByIdAsync(userId, cancellationToken);
                if (cashier != null && !cashier.CanRefund)
                {
                    return Results.Forbid();
                }

                var hasShift = await shiftService.HasActiveShiftAsync(userId);
                if (!hasShift) return Results.BadRequest(new { error = "Shift not started" });
            }

            var result = await orderService.CancelOrderAsync(id, request?.Reason);
            if (!result) return Results.NotFound();

            var updated = await orderService.GetOrderByIdAsync(id);
            return Results.Ok(updated);
        })
        .WithName("RefundOrder")
        .WithSummary("Refund an order - allowed for admins or the cashier who created the order")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization();

        // Get order statistics
        group.MapGet("/statistics", async (
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromServices] IOrderService orderService,
            CancellationToken cancellationToken) =>
        {
            var stats = await orderService.GetOrderStatisticsAsync(null, fromDate, toDate);
            return Results.Ok(stats);
        })
        .WithName("GetOrderStatistics")
        .WithSummary("Get order statistics - Requires orders.read permission")
        .Produces<OrderStatisticsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.OrdersRead));

        // Get my statistics (Cashier own stats)
        group.MapGet("/my-statistics", async (
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromServices] IOrderService orderService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var stats = await orderService.GetOrderStatisticsAsync(userId, fromDate, toDate);
            return Results.Ok(stats);
        })
        .WithName("GetMyOrderStatistics")
        .WithSummary("Get current user's order statistics - Requires orders.read.own permission")
        .Produces<OrderStatisticsDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .RequireAuthorization()
        .WithMetadata(new RequirePermissionAttribute(Permissions.OrdersReadOwn));
    }
}

public class CancelOrderRequest
{
    public string? Reason { get; set; }
}

public class RefundOrderRequest
{
    public string? Reason { get; set; }
}

public class PartialRefundRequest
{
    public decimal Amount { get; set; }
    public List<PartialRefundItemRequest>? Items { get; set; }
    // UTC time the refund was performed on the client. Used by offline-replayed
    // refunds so the server records the actual refund time, not the sync time.
    public DateTime? ClientCreatedAt { get; set; }
}

public class PartialRefundItemRequest
{
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
}
