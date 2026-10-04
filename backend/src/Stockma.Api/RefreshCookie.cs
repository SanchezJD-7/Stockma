namespace Stockma.Api;

public static class RefreshCookie
{
    public const string Name = "stockma_refresh";
    public const string Path = "/api/auth";

    public static string BuildSetCookieValue(string token) =>
        $"{Name}={token}; HttpOnly; Secure; SameSite=Strict; Path={Path}";

    public static string BuildClearCookieValue() =>
        $"{Name}=; HttpOnly; Secure; SameSite=Strict; Path={Path}; Max-Age=0";
}
