namespace Domain.Constants;

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string BranchManager = "BranchManager";

    // Kept for backward compatibility if referenced elsewhere but ideally should be removed or deprecated
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Support = "Support";

    public static readonly string[] All =
    {
        Administrator,
        BranchManager
    };

    public static bool IsValid(string role) => All.Contains(role);
}
