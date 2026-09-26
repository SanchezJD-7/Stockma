using Stockma.Application.Common;
using Stockma.Application.Identity;

namespace Stockma.Application.Tests;

public sealed class FakeUserAccounts : IUserAccounts
{
    public LoginIdentity? CredentialsResult { get; set; }
    public LoginIdentity? ByEmailResult { get; set; }
    public bool EmailTaken { get; set; }
    public NewUser? Created { get; private set; }
    public Guid CreatedId { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public Task<LoginIdentity?> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) => Task.FromResult(CredentialsResult);

    public Task<LoginIdentity?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(ByEmailResult);

    public Task<Guid> CreateAsync(NewUser user, CancellationToken cancellationToken = default)
    {
        Created = user;
        return Task.FromResult(CreatedId);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmailTaken);
}

public sealed class FakeTrustedDevices : ITrustedDevices
{
    public bool Trusted { get; set; }
    public bool TrustGranted { get; set; } = true;
    public List<string> TrustAttempts { get; } = [];
    public Task<bool> IsTrustedAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default) => Task.FromResult(Trusted);

    public Task<bool> TryTrustAsync(
        Guid userId,
        string deviceId,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        TrustAttempts.Add(deviceId);
        return Task.FromResult(TrustGranted);
    }
}

public sealed class FakeDeviceOtpService : IDeviceOtpService
{
    public List<(Guid UserId, string DeviceId)> Issued { get; } = [];
    public List<(Guid UserId, string DeviceId, string Code)> Consumed { get; } = [];
    public Exception? ConsumeThrows { get; set; }
    public Exception? IssueThrows { get; set; }
    public Task IssueAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        if (IssueThrows is not null)
        {
            throw IssueThrows;
        }

        Issued.Add((userId, deviceId));
        return Task.CompletedTask;
    }

    public Task ConsumeAsync(
        Guid userId,
        string deviceId,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (ConsumeThrows is not null)
        {
            throw ConsumeThrows;
        }

        Consumed.Add((userId, deviceId, code));
        return Task.CompletedTask;
    }
}

public sealed class FakeJwtTokenService : IJwtTokenService
{
    public List<(Guid UserId, Guid TenantId, IReadOnlyCollection<string> Roles)> Requests { get; } = [];
    public AccessToken Create(Guid userId, Guid tenantId, IReadOnlyCollection<string> roles)
    {
        Requests.Add((userId, tenantId, roles));
        return new AccessToken($"jwt-para-{userId}", 3600);
    }
}

public sealed class FakeTenantContext(Guid tenantId) : ITenantContext
{
    public Guid TenantId { get; private set; } = tenantId;
    public bool IsResolved => TenantId != Guid.Empty;
    public List<Guid> SetCalls { get; } = [];
    public void Set(Guid value)
    {
        SetCalls.Add(value);
        TenantId = value;
    }
}
