namespace Web.Models.Constants;

public static class Permissions
{
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";
    public const string UsersDelete = "users.delete";

    public const string RolesRead = "roles.read";
    public const string RolesWrite = "roles.write";
    public const string RolesDelete = "roles.delete";

    public const string SystemAudit = "system.audit";

    public const string LookupsRead = "lookups.read";
    public const string LookupsWrite = "lookups.write";
    public const string LookupsDelete = "lookups.delete";

    public const string CategoriesRead = "categories.read";
    public const string CategoriesWrite = "categories.write";
    public const string CategoriesDelete = "categories.delete";

    public const string ProductsRead = "products.read";
    public const string ProductsWrite = "products.write";
    public const string ProductsDelete = "products.delete";

    public const string SuppliersRead = "suppliers.read";
    public const string SuppliersWrite = "suppliers.write";
    public const string SuppliersDelete = "suppliers.delete";

    public const string RequestsRead = "requests.read";
    public const string RequestsWrite = "requests.write";

    public const string BranchesRead = "branches.read";
    public const string BranchesWrite = "branches.write";
    public const string BranchesDelete = "branches.delete";

    public const string WarehousesRead = "warehouses.read";
    public const string WarehousesWrite = "warehouses.write";
    public const string WarehousesDelete = "warehouses.delete";

    public const string TerminalsRead = "terminals.read";
    public const string TerminalsWrite = "terminals.write";
    public const string TerminalsDelete = "terminals.delete";

    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string InventoryApprove = "inventory.approve";
    public const string InventoryDelete = "inventory.delete";

    public const string PurchaseRequestsRead = "purchase-requests.read";
    public const string PurchaseRequestsWrite = "purchase-requests.write";
    public const string PurchaseRequestsApprove = "purchase-requests.approve";
    public const string PurchaseRequestsConvert = "purchase-requests.convert";

    public const string UnitsRead = "units.read";
    public const string UnitsWrite = "units.write";
    public const string UnitsDelete = "units.delete";

    public const string AssistantKeywordsRead = "assistant.keywords.read";
    public const string AssistantKeywordsWrite = "assistant.keywords.write";
    public const string AssistantKeywordsApprove = "assistant.keywords.approve";

    public static readonly string[] All =
    {
        UsersRead, UsersWrite, UsersDelete,
        RolesRead, RolesWrite, RolesDelete,
        SystemAudit,
        LookupsRead, LookupsWrite, LookupsDelete,
        CategoriesRead, CategoriesWrite, CategoriesDelete,
        ProductsRead, ProductsWrite, ProductsDelete,
        SuppliersRead, SuppliersWrite, SuppliersDelete,
        RequestsRead, RequestsWrite,
        BranchesRead, BranchesWrite, BranchesDelete,
        WarehousesRead, WarehousesWrite, WarehousesDelete,
        TerminalsRead, TerminalsWrite, TerminalsDelete,
        InventoryRead, InventoryWrite, InventoryApprove, InventoryDelete,
        UnitsRead, UnitsWrite, UnitsDelete,
        AssistantKeywordsRead, AssistantKeywordsWrite, AssistantKeywordsApprove
    };

    public static bool IsValid(string permission) => All.Contains(permission);
}

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Support = "Support";
    public const string BranchManager = "BranchManager";

    public static readonly string[] All =
    {
        Administrator,
        BranchManager
    };

    public static bool IsValid(string role) => All.Contains(role);
}
