namespace Stockma.Api;

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const int PermitPerWindow = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
