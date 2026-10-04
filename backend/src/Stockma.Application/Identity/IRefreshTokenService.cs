namespace Stockma.Application.Identity;
public interface IRefreshTokenService
{
    Task<string> IssueNewFamilyAsync(Guid tenantId,Guid userId,string deviceId,CancellationToken cancellationToken = default);
    Task<RefreshTokenSession> RotateAsync(string presentedToken,string deviceId,CancellationToken cancellationToken = default);
    Task RevokeFamilyAsync(string presentedToken, CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenSession(
    string PlainToken,
    Guid UserId,
    Guid TenantId,
    IReadOnlyList<string> Roles);
