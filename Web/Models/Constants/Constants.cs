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

    public const string AssistantKeywordsRead = "assistant.keywords.read";
    public const string AssistantKeywordsWrite = "assistant.keywords.write";
    public const string AssistantKeywordsApprove = "assistant.keywords.approve";

    public const string AppsRead = "apps.read";
    public const string AppsWrite = "apps.write";

    public static readonly string[] All =
    {
        UsersRead, UsersWrite, UsersDelete,
        RolesRead, RolesWrite, RolesDelete,
        SystemAudit,
        LookupsRead, LookupsWrite, LookupsDelete,
        AssistantKeywordsRead, AssistantKeywordsWrite, AssistantKeywordsApprove,
        AppsRead, AppsWrite
    };

    public static bool IsValid(string permission) => All.Contains(permission);
}

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Support = "Support";

    public static readonly string[] All =
    {
        Administrator
    };

    public static bool IsValid(string role) => All.Contains(role);
}
