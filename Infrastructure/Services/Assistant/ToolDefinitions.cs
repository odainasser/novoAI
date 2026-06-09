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

        // ── Products ──────────────────────────────────────────────────
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

        // ── Suppliers / categories / units ────────────────────────────
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

        // ── Approval requests / purchase requests ─────────────────────
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
            "A company-wide KPI summary (catalog and inventory health). Company-wide.",
            crossBranch: true,
            exec: async (args, ctx) =>
            {
                var summary = await ctx.Sp.GetRequiredService<IDashboardService>().GetSummaryAsync();
                return new ToolResult(summary);
            }));

        // ── Composite MIXING tools (two datasets joined in code) ───────
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
            ["list_products"] = ("Products", new[] { "Product" }),
            ["count_products"] = ("Products", new[] { "Product" }),
            ["list_low_stock"] = ("Inventory", new[] { "StockBalance", "Product" }),
            ["list_stock"] = ("Inventory", new[] { "StockBalance", "Product" }),
            ["list_suppliers"] = ("Suppliers", new[] { "Supplier" }),
            ["list_categories"] = ("Catalog", new[] { "Category" }),
            ["list_units"] = ("Catalog", new[] { "Unit" }),
            ["list_approval_requests"] = ("Requests", new[] { "Request" }),
            ["list_purchase_requests"] = ("Purchasing", new[] { "PurchaseRequest" }),
            ["get_my_profile"] = ("Identity", new[] { "User" }),
            ["get_my_permissions"] = ("Identity", new[] { "User" }),
            ["get_my_roles"] = ("Identity", new[] { "Role" }),
            ["get_my_branches"] = ("Identity", new[] { "Branch" }),
            ["list_users"] = ("Administration", new[] { "User" }),
            ["list_roles"] = ("Administration", new[] { "Role" }),
            ["list_warehouses"] = ("Warehouses", new[] { "Warehouse" }),
            ["list_branches"] = ("Branches", new[] { "Branch" }),
            ["get_business_overview"] = ("Overview", new[] { "Dashboard" }),
            ["suppliers_with_pending_purchases"] = ("Purchasing", new[] { "Supplier", "PurchaseRequest" }),
        };

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
