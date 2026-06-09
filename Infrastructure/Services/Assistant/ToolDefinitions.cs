using Application.Common.Interfaces;
using Application.Services;
using Domain.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Assistant;

/// <summary>
/// The single, code-owned declaration of every read tool the assistant exposes.
/// Each tool wraps an existing read-only service call (the same calls the rest of
/// the system uses), pre-shapes the result, and declares the permission(s) and
/// branch behaviour the catalog/orchestrator enforce. The model chooses tools and
/// arguments; this file is where the application keeps ownership of every query.
/// </summary>
internal static class ToolDefinitions
{
    public static List<IAssistantTool> BuildAll()
    {
        var tools = new List<IAssistantTool>();

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

        // ── Facilities ────────────────────────────────────────────────
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
            "A company-wide KPI summary (facilities and identity counts). Company-wide.",
            crossBranch: true,
            exec: async (args, ctx) =>
            {
                var summary = await ctx.Sp.GetRequiredService<IDashboardService>().GetSummaryAsync();
                return new ToolResult(summary);
            }));

        // Tag every tool with its business module (domain) + the entities it reads.
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
            ["get_my_profile"] = ("Identity", new[] { "User" }),
            ["get_my_permissions"] = ("Identity", new[] { "User" }),
            ["get_my_roles"] = ("Identity", new[] { "Role" }),
            ["get_my_branches"] = ("Identity", new[] { "Branch" }),
            ["list_users"] = ("Administration", new[] { "User" }),
            ["list_roles"] = ("Administration", new[] { "Role" }),
            ["list_warehouses"] = ("Warehouses", new[] { "Warehouse" }),
            ["list_branches"] = ("Branches", new[] { "Branch" }),
            ["get_business_overview"] = ("Overview", new[] { "Dashboard" }),
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

    // ── JSON-schema fragment builders ─────────────────────────────────
    private static object ObjSchema(params (string Name, object Schema)[] props)
    {
        var dict = new Dictionary<string, object>();
        foreach (var (n, s) in props) dict[n] = s;
        return new { type = "object", properties = dict };
    }

    private static object StrSchema(string description) => new { type = "string", description };
    private static object LimitSchema() => new { type = "integer", description = "Max rows to return (1-50, default 10)." };
}
