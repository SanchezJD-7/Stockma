namespace Stockma.Application.Identity;

public sealed record LoginResult
{
    private LoginResult()
    {
    }

    public string? AccessToken { get; private init; }
    public int? ExpiresIn { get; private init; }
    public bool RequiresDeviceConfirmation { get; private init; }
    public string? RefreshToken { get; private init; }
    public static LoginResult Issued(AccessToken token, string refreshToken) => new()
    {
        AccessToken = token.Value,
        ExpiresIn = token.ExpiresInSeconds,
        RefreshToken = refreshToken,
    };

    public static LoginResult NeedsDeviceConfirmation() => new()
    {
        RequiresDeviceConfirmation = true,
    };
}

public sealed record ConfirmDeviceResult(
    string AccessToken,
    int ExpiresIn,
    bool DeviceTrusted,
    string RefreshToken);
public sealed record RegisteredUser(Guid UserId, string Email);
