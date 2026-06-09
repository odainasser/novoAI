using Application.Common.Interfaces;
using Application.Services;
using Domain.Constants;
using Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// The single, code-owned declaration of every read tool the assistant exposes.
/// Each tool wraps an existing read-only service call (the same calls the rest of
/// the system uses), pre-shapes the result, and declares the permission(s) and
/// branch behaviour the catalog/orchestrator enforce. The model chooses tools and
/// arguments; this file is where the application keeps ownership of every query.
///
/// Mixing (multi-data) questions are served by dedicated COMPOSITE tools that fetch
/// both datasets and join them in code — the model never receives two raw lists.
/// </summary>
internal static class ToolDefinitions
{
    public static List<IAssistantTool> BuildAll()
    {
        var tools = new List<IAssistantTool>();

        // ── Sales & revenue ───────────────────────────────────────────
        tools.Add(new DelegateTool(
            "get_revenue",
            "Total sales revenue and order count for a period. Use for 'how much did we make', 'total sales/revenue'.",
            permissions: new[] { Permissions.OrdersRead },
            parametersSchema: ObjSchema(("period", ToolHelpers.PeriodParam("Time range; omit for all time."))),
            exec: async (args, ctx) =>
            {
                var period = ToolArgs.Str(args, "period");
                var (from, to) = ToolHelpers.ResolvePeriod(period);

                if (ctx.BranchLocked)
                {
                    var revenue = await BranchRevenueAsync(ctx, from, to);
                    var orders = (await ctx.Sp.GetRequiredService<IOrderService>().GetAllOrdersAsync(
                        1, 1, null, null, null, null, from, to, ctx.WarehouseId, null, ctx.BranchWarehouseIds)).TotalCount;
                    return new ToolResult(new
                    {
                        period = ToolHelpers.PeriodLabel(period),
                        totalRevenue = ToolHelpers.Aed(revenue),
                        totalOrders = orders,
                        averageOrderValue = orders > 0 ? ToolHelpers.Aed(Math.Round(revenue / orders, 2)) : ToolHelpers.Aed(0m)
                    });
                }

                var stats = await ctx.Sp.GetRequiredService<IOrderService>().GetOrderStatisticsAsync(null, from, to);
                return new ToolResult(new
                {
                    period = ToolHelpers.PeriodLabel(period),
                    totalRevenue = ToolHelpers.Aed(stats.TotalRevenue),
                    totalOrders = stats.TotalOrders,
                    completedOrders = stats.CompletedOrders,
                    averageOrderValue = stats.TotalOrders > 0 ? ToolHelpers.Aed(Math.Round(stats.TotalRevenue / stats.TotalOrders, 2)) : ToolHelpers.Aed(0m)
                });
            }));

        tools.Add(new DelegateTool(
            "get_monthly_revenue_trend",
            "Revenue per month for the last several months — use to compare or trend revenue over time. Company-wide.",
            permissions: new[] { Permissions.OrdersRead },
            crossBranch: true,
            exec: async (args, ctx) =>
            {
                var months = await ctx.Sp.GetRequiredService<IDashboardService>().GetMonthlyRevenueAsync(6);
                return new ToolResult(new { months });
            }));

        // ── Orders ────────────────────────────────────────────────────
        tools.Add(new DelegateTool(
            "count_orders",
            "How many orders (optionally filtered by status: completed/refunded) in a period.",
            permissions: new[] { Permissions.OrdersRead },
            parametersSchema: ObjSchema(
                ("period", ToolHelpers.PeriodParam("Time range; omit for all time.")),
                ("status", StatusSchema("completed", "refunded"))),
            exec: async (args, ctx) =>
            {
                var (from, to) = ToolHelpers.ResolvePeriod(ToolArgs.Str(args, "period"));
                var count = (await ctx.Sp.GetRequiredService<IOrderService>().GetAllOrdersAsync(
                    1, 1, null, OrderStatusOf(ToolArgs.Str(args, "status")), null, null,
                    from, to, ctx.WarehouseId, null, ctx.BranchWarehouseIds)).TotalCount;
                return new ToolResult(new { count, period = ToolHelpers.PeriodLabel(ToolArgs.Str(args, "period")) });
            }));

        tools.Add(new DelegateTool(
            "list_orders",
            "List recent orders (optionally by status/period). For browsing orders, not a single lookup.",
            permissions: new[] { Permissions.OrdersRead },
            parametersSchema: ObjSchema(
                ("period", ToolHelpers.PeriodParam("Time range; omit for all time.")),
                ("status", StatusSchema("completed", "refunded")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var (from, to) = ToolHelpers.ResolvePeriod(ToolArgs.Str(args, "period"));
                var page = await ctx.Sp.GetRequiredService<IOrderService>().GetAllOrdersAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), null,
                    OrderStatusOf(ToolArgs.Str(args, "status")), null, null,
                    from, to, ctx.WarehouseId, null, ctx.BranchWarehouseIds);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(new DelegateTool(
            "get_order_by_number",
            "Full detail of one order by its order number (e.g. ORD-1024).",
            permissions: new[] { Permissions.OrdersRead },
            parametersSchema: ObjSchema(("orderNumber", StrSchema("The order number, e.g. ORD-1024.")), required: "orderNumber"),
            exec: async (args, ctx) =>
            {
                var number = (ToolArgs.Str(args, "orderNumber") ?? "").Replace(" ", "").ToUpperInvariant();
                if (number.Length == 0) return new ToolResult(null);
                var order = await ctx.Sp.GetRequiredService<IOrderService>().GetOrderByNumberAsync(number);
                return new ToolResult(order);
            }));

        tools.Add(new DelegateTool(
            "list_returns",
            "List returned/refunded orders for a period.",
            permissions: new[] { Permissions.OrdersRead },
            parametersSchema: ObjSchema(
                ("period", ToolHelpers.PeriodParam("Time range; omit for all time.")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var (from, to) = ToolHelpers.ResolvePeriod(ToolArgs.Str(args, "period"));
                var page = await ctx.Sp.GetRequiredService<IOrderService>().GetAllOrdersAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), null, OrderStatus.Refunded, null, null,
                    from, to, ctx.WarehouseId, null, ctx.BranchWarehouseIds);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        // ── Products ──────────────────────────────────────────────────
        tools.Add(new DelegateTool(
            "top_selling_products",
            "The best-selling products by quantity sold in a period.",
            permissions: new[] { Permissions.ProductsRead },
            parametersSchema: ObjSchema(
                ("period", ToolHelpers.PeriodParam("Time range; omit for all time.")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var (from, to) = ToolHelpers.ResolvePeriod(ToolArgs.Str(args, "period"));
                var top = await TopProductsAsync(ctx, from, to, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")));
                return new ToolResult(new { period = ToolHelpers.PeriodLabel(ToolArgs.Str(args, "period")), products = top });
            }));

        tools.Add(new DelegateTool(
            "list_products",
            "List or search products in the catalog (optionally by active/inactive status).",
            permissions: new[] { Permissions.ProductsRead },
            parametersSchema: ObjSchema(
                ("search", StrSchema("Filter by product name/code.")),
                ("status", StatusSchema("active", "inactive")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var status = ToolArgs.Str(args, "status");
                var page = await ctx.Sp.GetRequiredService<IProductService>().GetAllProductsAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), search: ToolArgs.Str(args, "search"),
                    isActive: IsActiveOf(status), status: ItemStatusOf(status));
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(new DelegateTool(
            "count_products",
            "How many products exist (optionally filtered by active/inactive).",
            permissions: new[] { Permissions.ProductsRead },
            parametersSchema: ObjSchema(("status", StatusSchema("active", "inactive"))),
            exec: async (args, ctx) =>
            {
                var status = ToolArgs.Str(args, "status");
                var count = (await ctx.Sp.GetRequiredService<IProductService>().GetAllProductsAsync(
                    1, 1, search: null, isActive: IsActiveOf(status), status: ItemStatusOf(status))).TotalCount;
                return new ToolResult(new { count });
            }));

        // ── Inventory / stock ─────────────────────────────────────────
        tools.Add(new DelegateTool(
            "list_low_stock",
            "Products that are low on stock or out of stock right now.",
            permissions: new[] { Permissions.InventoryRead },
            parametersSchema: ObjSchema(
                ("onlyOutOfStock", BoolSchema("True for out-of-stock only; otherwise low stock (incl. out of stock).")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var status = ToolArgs.Bool(args, "onlyOutOfStock") == true ? "outofstock" : "lowstock";
                var page = await ctx.Sp.GetRequiredService<IInventoryHistoryService>().GetAllStockBalancesAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), null, ctx.WarehouseId, status, ctx.BranchWarehouseIds);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(new DelegateTool(
            "list_stock",
            "Current stock balances (on-hand quantities), optionally filtered by product name.",
            permissions: new[] { Permissions.InventoryRead },
            parametersSchema: ObjSchema(
                ("search", StrSchema("Filter by product name/code.")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IInventoryHistoryService>().GetAllStockBalancesAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), ToolArgs.Str(args, "search"), ctx.WarehouseId, null, ctx.BranchWarehouseIds);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        // ── Suppliers / categories / promotions / units ──────────────
        tools.Add(SimpleListTool(
            "list_suppliers", "List or search suppliers.", Permissions.SuppliersRead,
            async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<ISupplierService>().GetAllSuppliersAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), ToolArgs.Str(args, "search"), null);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(SimpleListTool(
            "list_categories", "List or search product categories.", Permissions.CategoriesRead,
            async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<ICategoryService>().GetAllCategoriesAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), null, ToolArgs.Str(args, "search"), null);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(SimpleListTool(
            "list_units", "List or search units / SKUs.", Permissions.UnitsRead,
            async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IUnitService>().GetAllAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), search: ToolArgs.Str(args, "search"), isActive: null, status: null);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(new DelegateTool(
            "list_promotions",
            "List promotions. By default returns the currently-active promotions.",
            permissions: new[] { Permissions.PromotionsRead },
            parametersSchema: ObjSchema(("activeOnly", BoolSchema("True (default) for active promotions only."))),
            exec: async (args, ctx) =>
            {
                var promos = ctx.Sp.GetRequiredService<IPromotionService>();
                if (ToolArgs.Bool(args, "activeOnly") != false)
                {
                    var active = await promos.GetActivePromotionsAsync();
                    return new ToolResult(new { totalCount = active.Count, items = active });
                }
                var page = await promos.GetAllPromotionsAsync(1, 20, null, null);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        // ── Shifts / approval requests / purchase requests ────────────
        tools.Add(new DelegateTool(
            "list_shifts",
            "List cashier shifts (optionally by status active/completed and period).",
            permissions: new[] { Permissions.ShiftsRead },
            parametersSchema: ObjSchema(
                ("period", ToolHelpers.PeriodParam("Time range; omit for all time.")),
                ("status", StatusSchema("active", "completed")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var (from, to) = ToolHelpers.ResolvePeriod(ToolArgs.Str(args, "period"));
                var page = await ctx.Sp.GetRequiredService<IShiftService>().GetAllShiftsAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), ShiftStatusOf(ToolArgs.Str(args, "status")),
                    null, null, ctx.WarehouseId, ctx.BranchWarehouseIds, from, to);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(new DelegateTool(
            "list_approval_requests",
            "List approval requests (optionally by status pending/approved/rejected).",
            permissions: new[] { Permissions.RequestsRead },
            parametersSchema: ObjSchema(
                ("status", StatusSchema("pending", "approved", "rejected")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IRequestService>().GetAllRequestsAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), null, null, RequestStatusOf(ToolArgs.Str(args, "status")));
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(new DelegateTool(
            "list_purchase_requests",
            "List purchase requests / purchase orders (optionally by status; default submitted/pending).",
            permissions: new[] { Permissions.PurchaseRequestsRead },
            parametersSchema: ObjSchema(
                ("status", StatusSchema("draft", "submitted", "approved", "rejected")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var status = PurchaseStatusOf(ToolArgs.Str(args, "status")) ?? PurchaseRequestStatus.Submitted;
                var limit = ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit"));
                var svc = ctx.Sp.GetRequiredService<IPurchaseRequestService>();
                var page = ctx.BranchLocked
                    ? await svc.GetByWarehouseIdsAsync(ctx.BranchWarehouseIds ?? Array.Empty<Guid>(), 1, limit, status, null)
                    : await svc.GetAllAsync(1, limit, null, status, null, null, null, null);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        // ── Identity (self) & administration ──────────────────────────
        tools.Add(new DelegateTool(
            "get_my_profile",
            "The current signed-in user's own profile (name, email). Use for 'who am I'.",
            exec: async (args, ctx) =>
            {
                if (ctx.UserGuid is null) return new ToolResult(null);
                var user = await ctx.Sp.GetRequiredService<IUserService>().GetUserByIdAsync(ctx.UserGuid.Value, ctx.Ct);
                return new ToolResult(user);
            }));

        tools.Add(new DelegateTool(
            "get_my_permissions",
            "The current user's own permissions / access rights.",
            exec: async (args, ctx) =>
            {
                if (ctx.UserGuid is null) return new ToolResult(null);
                var perms = await ctx.Sp.GetRequiredService<IIdentityService>().GetUserPermissionsAsync(ctx.UserGuid.Value);
                return new ToolResult(new { permissions = perms });
            }));

        tools.Add(new DelegateTool(
            "get_my_roles",
            "The current user's own role(s).",
            exec: async (args, ctx) =>
            {
                if (ctx.UserGuid is null) return new ToolResult(null);
                var roles = await ctx.Sp.GetRequiredService<IIdentityService>().GetUserRolesAsync(ctx.UserGuid.Value);
                return new ToolResult(new { roles });
            }));

        tools.Add(new DelegateTool(
            "get_my_branches",
            "The branches assigned to the current user.",
            exec: async (args, ctx) =>
            {
                if (ctx.UserGuid is null) return new ToolResult(null);
                var mine = await ctx.Sp.GetRequiredService<IBranchService>().GetBranchesAssignedToUserAsync(ctx.UserGuid.Value);
                return new ToolResult(new { items = mine });
            }));

        tools.Add(SimpleListTool(
            "list_users", "List or search system users / staff.", Permissions.UsersRead,
            async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IUserService>().GetAllUsersAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), null, ToolArgs.Str(args, "search"), null, ctx.Ct);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(new DelegateTool(
            "list_roles",
            "List the system roles.",
            permissions: new[] { Permissions.RolesRead },
            exec: async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IRoleService>().GetAllRolesAsync(1, 50, ctx.Ct);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        tools.Add(SimpleListTool(
            "list_cashiers", "List or search cashiers.", Permissions.CashiersRead,
            async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<ICashierService>().GetAllCashiersAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), ToolArgs.Str(args, "search"),
                    ctx.BranchLocked ? true : (bool?)null, ctx.WarehouseId, ctx.Ct, ctx.BranchWarehouseIds);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            }));

        // ── Company-wide (withheld under a branch lock) ───────────────
        tools.Add(SimpleListTool(
            "list_warehouses", "List the company's warehouses.", Permissions.WarehousesRead,
            async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IWarehouseService>().GetAllWarehousesAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), ToolArgs.Str(args, "search"), null);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            },
            crossBranch: true));

        tools.Add(SimpleListTool(
            "list_branches", "List the company's branches.", Permissions.BranchesRead,
            async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IBranchService>().GetAllBranchesAsync(
                    1, ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit")), ToolArgs.Str(args, "search"), null);
                return new ToolResult(new { totalCount = page.TotalCount, items = page.Items });
            },
            crossBranch: true));

        tools.Add(new DelegateTool(
            "get_business_overview",
            "A company-wide KPI summary (overall sales, orders, inventory health). Company-wide.",
            crossBranch: true,
            exec: async (args, ctx) =>
            {
                var summary = await ctx.Sp.GetRequiredService<IDashboardService>().GetSummaryAsync();
                return new ToolResult(summary);
            }));

        // ── Composite MIXING tools (two datasets joined in code) ───────
        tools.Add(new DelegateTool(
            "low_stock_products_with_no_sales",
            "Products that are LOW ON STOCK and had NO sales in the period — a combined (mixing) report. Use when both conditions are asked together.",
            permissions: new[] { Permissions.InventoryRead, Permissions.ProductsRead },
            isMixing: true,
            parametersSchema: ObjSchema(
                ("period", ToolHelpers.PeriodParam("Sales window to check for 'no sales'; omit for all time.")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var (from, to) = ToolHelpers.ResolvePeriod(ToolArgs.Str(args, "period"));
                var limit = ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit"));

                var low = await ctx.Sp.GetRequiredService<IInventoryHistoryService>()
                    .GetAllStockBalancesAsync(1, 500, null, ctx.WarehouseId, "lowstock", ctx.BranchWarehouseIds);
                var stats = await ctx.Sp.GetRequiredService<IDashboardService>().GetWarehouseProductStatsAsync(from, to);
                var soldIds = stats.Where(w => InBranch(ctx, w.WarehouseId))
                    .SelectMany(w => w.Products).Where(p => p.QuantitySold > 0)
                    .Select(p => p.ProductId).ToHashSet();

                var all = low.Items.Where(s => !soldIds.Contains(s.ProductId))
                    .GroupBy(s => s.ProductId)
                    .Select(g => new
                    {
                        product = g.First().ProductNameEn,
                        productAr = g.First().ProductNameAr,
                        currentStock = g.Sum(x => x.AvailableQuantity),
                        lowStockThreshold = g.First().LowStockThreshold
                    })
                    .OrderBy(r => r.currentStock)
                    .ToList();

                return new ToolResult(MixPayload(ToolArgs.Str(args, "period"), all, limit));
            }));

        tools.Add(new DelegateTool(
            "top_products_with_stock",
            "Top-selling products in a period WITH their current stock level — a combined (mixing) report joining sales and inventory.",
            permissions: new[] { Permissions.ProductsRead, Permissions.InventoryRead },
            isMixing: true,
            parametersSchema: ObjSchema(
                ("period", ToolHelpers.PeriodParam("Sales window; omit for all time.")),
                ("limit", LimitSchema())),
            exec: async (args, ctx) =>
            {
                var (from, to) = ToolHelpers.ResolvePeriod(ToolArgs.Str(args, "period"));
                var limit = ToolHelpers.ClampLimit(ToolArgs.Int(args, "limit"));

                var top = await TopProductsAsync(ctx, from, to, limit);
                var stock = await ctx.Sp.GetRequiredService<IInventoryHistoryService>()
                    .GetAllStockBalancesAsync(1, 500, null, ctx.WarehouseId, null, ctx.BranchWarehouseIds);
                var stockByProduct = stock.Items.GroupBy(s => s.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.AvailableQuantity));

                var rows = top.Select(p => new
                {
                    product = p.Product,
                    productAr = p.ProductAr,
                    sold = p.QuantitySold,
                    currentStock = stockByProduct.TryGetValue(p.ProductId, out var s) ? s : 0
                }).ToList();

                return new ToolResult(new
                {
                    period = ToolHelpers.PeriodLabel(ToolArgs.Str(args, "period")),
                    total = rows.Count,
                    shown = rows.Count,
                    truncated = false,
                    rows
                });
            }));

        tools.Add(new DelegateTool(
            "suppliers_with_pending_purchases",
            "Suppliers that have pending (submitted) purchase requests, with how many and the total items requested — a combined (mixing) report. Company-wide.",
            permissions: new[] { Permissions.SuppliersRead, Permissions.PurchaseRequestsRead },
            isMixing: true,
            crossBranch: true,
            exec: async (args, ctx) =>
            {
                var page = await ctx.Sp.GetRequiredService<IPurchaseRequestService>()
                    .GetAllAsync(1, 200, null, PurchaseRequestStatus.Submitted, null, null, null, null);

                var rows = page.Items.Where(p => p.SupplierId.HasValue)
                    .GroupBy(p => p.SupplierId!.Value)
                    .Select(g => new
                    {
                        supplier = g.First().SupplierNameEn,
                        supplierAr = g.First().SupplierNameAr,
                        pendingPurchaseRequests = g.Count(),
                        totalItemsRequested = g.Sum(x => x.TotalItems)
                    })
                    .OrderByDescending(r => r.pendingPurchaseRequests)
                    .ToList();

                return new ToolResult(new { total = rows.Count, shown = rows.Count, truncated = false, rows });
            }));

        // Tag every tool with its business module (domain) + the entities it reads.
        // This is the single, code-owned source the admin "plan" dropdowns read from.
        foreach (var t in tools)
            if (t is DelegateTool dt && Meta.TryGetValue(dt.Name, out var m))
            {
                dt.Domain = m.Domain;
                dt.Entities = m.Entities;
            }

        return tools;
    }

    // Code-owned plan metadata: tool name → (module/domain, entities it touches).
    private static readonly IReadOnlyDictionary<string, (string Domain, string[] Entities)> Meta =
        new Dictionary<string, (string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["get_revenue"] = ("Sales", new[] { "Order" }),
            ["get_monthly_revenue_trend"] = ("Sales", new[] { "Order" }),
            ["count_orders"] = ("Orders", new[] { "Order" }),
            ["list_orders"] = ("Orders", new[] { "Order" }),
            ["get_order_by_number"] = ("Orders", new[] { "Order" }),
            ["list_returns"] = ("Orders", new[] { "Order" }),
            ["top_selling_products"] = ("Products", new[] { "Product" }),
            ["list_products"] = ("Products", new[] { "Product" }),
            ["count_products"] = ("Products", new[] { "Product" }),
            ["list_low_stock"] = ("Inventory", new[] { "StockBalance", "Product" }),
            ["list_stock"] = ("Inventory", new[] { "StockBalance", "Product" }),
            ["list_suppliers"] = ("Suppliers", new[] { "Supplier" }),
            ["list_categories"] = ("Catalog", new[] { "Category" }),
            ["list_units"] = ("Catalog", new[] { "Unit" }),
            ["list_promotions"] = ("Promotions", new[] { "Promotion" }),
            ["list_shifts"] = ("Shifts", new[] { "Shift" }),
            ["list_approval_requests"] = ("Requests", new[] { "Request" }),
            ["list_purchase_requests"] = ("Purchasing", new[] { "PurchaseRequest" }),
            ["get_my_profile"] = ("Identity", new[] { "User" }),
            ["get_my_permissions"] = ("Identity", new[] { "User" }),
            ["get_my_roles"] = ("Identity", new[] { "Role" }),
            ["get_my_branches"] = ("Identity", new[] { "Branch" }),
            ["list_users"] = ("Administration", new[] { "User" }),
            ["list_roles"] = ("Administration", new[] { "Role" }),
            ["list_cashiers"] = ("Administration", new[] { "Cashier" }),
            ["list_warehouses"] = ("Warehouses", new[] { "Warehouse" }),
            ["list_branches"] = ("Branches", new[] { "Branch" }),
            ["get_business_overview"] = ("Overview", new[] { "Dashboard" }),
            ["low_stock_products_with_no_sales"] = ("Inventory", new[] { "Product", "StockBalance", "Order" }),
            ["top_products_with_stock"] = ("Products", new[] { "Product", "StockBalance" }),
            ["suppliers_with_pending_purchases"] = ("Purchasing", new[] { "Supplier", "PurchaseRequest" }),
        };

    // ── Shared fetch helpers (ported from the previous query mapper) ──

    private sealed record TopProductRow(Guid ProductId, string Product, string ProductAr, int QuantitySold);

    private static async Task<List<TopProductRow>> TopProductsAsync(ToolContext ctx, DateTime? from, DateTime? to, int limit)
    {
        var stats = await ctx.Sp.GetRequiredService<IDashboardService>().GetWarehouseProductStatsAsync(from, to);
        return stats
            .Where(w => InBranch(ctx, w.WarehouseId))
            .SelectMany(w => w.Products)
            .GroupBy(p => new { p.ProductId, p.ProductName, p.ProductNameAr })
            .Select(g => new TopProductRow(g.Key.ProductId, g.Key.ProductName, g.Key.ProductNameAr, g.Sum(x => x.QuantitySold)))
            .OrderByDescending(r => r.QuantitySold)
            .Take(limit)
            .ToList();
    }

    private static async Task<decimal> BranchRevenueAsync(ToolContext ctx, DateTime? from, DateTime? to)
    {
        var stats = await ctx.Sp.GetRequiredService<IDashboardService>().GetWarehouseProductStatsAsync(from, to);
        return stats.Where(w => InBranch(ctx, w.WarehouseId)).Sum(w => w.Revenue);
    }

    private static bool InBranch(ToolContext ctx, Guid warehouseId) =>
        ctx.BranchWarehouseIds is null || ctx.BranchWarehouseIds.Contains(warehouseId);

    // Cap + "shown X of Y" indicator for a mixing row set.
    private static object MixPayload<T>(string? period, IReadOnlyList<T> all, int limit)
    {
        var shown = all.Take(limit).ToList();
        return new
        {
            period = ToolHelpers.PeriodLabel(period),
            total = all.Count,
            shown = shown.Count,
            truncated = all.Count > shown.Count,
            rows = shown
        };
    }

    // ── A list tool with the common search+limit schema ───────────────
    private static DelegateTool SimpleListTool(
        string name, string description, string permission,
        Func<System.Text.Json.JsonElement, ToolContext, Task<ToolResult>> exec,
        bool crossBranch = false) =>
        new(name, description, exec,
            permissions: new[] { permission },
            parametersSchema: ObjSchema(("search", StrSchema("Optional text filter.")), ("limit", LimitSchema())),
            crossBranch: crossBranch);

    // ── Status converters (ported) ────────────────────────────────────
    private static OrderStatus? OrderStatusOf(string? s) => s switch
    {
        "completed" => OrderStatus.Completed,
        "refunded" => OrderStatus.Refunded,
        _ => null
    };

    private static RequestStatus? RequestStatusOf(string? s) => s switch
    {
        "pending" => RequestStatus.Pending,
        "approved" => RequestStatus.Approved,
        "rejected" => RequestStatus.Rejected,
        _ => null
    };

    private static PurchaseRequestStatus? PurchaseStatusOf(string? s) => s switch
    {
        "draft" => PurchaseRequestStatus.Draft,
        "submitted" or "pending" => PurchaseRequestStatus.Submitted,
        "approved" => PurchaseRequestStatus.Approved,
        "rejected" => PurchaseRequestStatus.Rejected,
        _ => null
    };

    private static ItemStatus? ItemStatusOf(string? s) => s switch
    {
        "active" => ItemStatus.Active,
        "inactive" => ItemStatus.Inactive,
        _ => null
    };

    private static bool? IsActiveOf(string? s) => s switch
    {
        "active" => true,
        "inactive" => false,
        _ => null
    };

    private static string? ShiftStatusOf(string? s) => s switch
    {
        "active" => nameof(ShiftStatus.Active),
        "completed" => nameof(ShiftStatus.Completed),
        _ => null
    };

    // ── JSON-schema fragment builders ─────────────────────────────────
    private static object ObjSchema(params (string Name, object Schema)[] props)
    {
        var dict = new Dictionary<string, object>();
        foreach (var (n, s) in props) dict[n] = s;
        return new { type = "object", properties = dict };
    }

    // Overload allowing a single required field alongside one property.
    private static object ObjSchema((string Name, object Schema) only, string required) =>
        new { type = "object", properties = new Dictionary<string, object> { [only.Name] = only.Schema }, required = new[] { required } };

    private static object StrSchema(string description) => new { type = "string", description };
    private static object BoolSchema(string description) => new { type = "boolean", description };
    private static object LimitSchema() => new { type = "integer", description = "Max rows to return (1-50, default 10)." };
    private static object StatusSchema(params string[] values) => new { type = "string", @enum = values, description = "Optional status filter." };
}
