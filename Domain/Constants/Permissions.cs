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
        AssistantKeywordsRead, AssistantKeywordsWrite, AssistantKeywordsApprove
    };

    public static bool IsValid(string permission) => All.Contains(permission);
}
