namespace Stockma.Application.Identity;

public static class TenantRoles
{
    public const string TenantAdmin = "TenantAdmin";
    public const string Member = "Member";
    public static readonly IReadOnlyList<string> All = [TenantAdmin, Member];
    public static bool IsKnown(string role) => All.Contains(role);
}
