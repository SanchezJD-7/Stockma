namespace Stockma.Application.Identity;

public interface IJwtTokenService
{
    AccessToken Create(Guid userId, Guid tenantId, IReadOnlyCollection<string> roles);
}

public sealed record AccessToken(string Value, int ExpiresInSeconds);
