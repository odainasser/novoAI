namespace Domain.Constants;

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

    // Branch Permissions
    public const string BranchesRead = "branches.read";
    public const string BranchesWrite = "branches.write";
    public const string BranchesDelete = "branches.delete";

    // Warehouse Permissions
    public const string WarehousesRead = "warehouses.read";
    public const string WarehousesWrite = "warehouses.write";
    public const string WarehousesDelete = "warehouses.delete";

    // Terminal Permissions
    public const string TerminalsRead = "terminals.read";
    public const string TerminalsWrite = "terminals.write";
    public const string TerminalsDelete = "terminals.delete";

    // AI Assistant keyword-dictionary management
    public const string AssistantKeywordsRead = "assistant.keywords.read";
    public const string AssistantKeywordsWrite = "assistant.keywords.write";
    public const string AssistantKeywordsApprove = "assistant.keywords.approve";

    public static readonly string[] All =
    {
        UsersRead, UsersWrite, UsersDelete,
        RolesRead, RolesWrite, RolesDelete,
        SystemAudit,
        LookupsRead, LookupsWrite, LookupsDelete,
        BranchesRead, BranchesWrite, BranchesDelete,
        WarehousesRead, WarehousesWrite, WarehousesDelete,
        TerminalsRead, TerminalsWrite, TerminalsDelete,
        AssistantKeywordsRead, AssistantKeywordsWrite, AssistantKeywordsApprove
    };

    public static bool IsValid(string permission) => All.Contains(permission);
}
