using Stockma.Application.Common;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Queries;
using Stockma.Application.Tenants;
using Stockma.Domain.Entities;
using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Tests;

public sealed class FakeUserAccounts : IUserAccounts
{
    public LoginIdentity? CredentialsResult { get; set; }
    public LoginIdentity? ByEmailResult { get; set; }
    public bool EmailTaken { get; set; }
    public NewUser? Created { get; private set; }
    public int CreateCalls { get; private set; }
    public Guid CreatedId { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public Task<LoginIdentity?> VerifyCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) => Task.FromResult(CredentialsResult);

    public Task<LoginIdentity?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(ByEmailResult);

    public LoginIdentity? ByIdResult { get; set; }

    public Task<LoginIdentity?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ByIdResult);

    public Func<bool>? LockProbe { get; set; }
    public bool? CheckedWhileLocked { get; private set; }
    public bool? CreatedWhileLocked { get; private set; }

    public Task<Guid> CreateAsync(NewUser user, CancellationToken cancellationToken = default)
    {
        Created = user;
        CreateCalls++;
        CreatedWhileLocked = LockProbe?.Invoke();
        return Task.FromResult(CreatedId);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(EmailTaken);

    public bool HasAnyUser { get; set; }

    public Task<bool> TenantHasAnyUserAsync(CancellationToken cancellationToken = default)
    {
        CheckedWhileLocked = LockProbe?.Invoke();
        return Task.FromResult(HasAnyUser);
    }
}

public sealed class FakeTenantAccounts : ITenantAccounts
{
    public bool Exists { get; set; }

    public Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Exists);

    public Guid? ProvisionedFor { get; private set; }
    public bool ProvisionedRequireSecondFactor { get; private set; }
    public int ProvisionCalls { get; private set; }

    public Task ProvisionAsync(
        Guid tenantId,
        bool requireSecondFactor,
        CancellationToken cancellationToken = default)
    {
        ProvisionedFor = tenantId;
        ProvisionedRequireSecondFactor = requireSecondFactor;
        ProvisionCalls++;
        return Task.CompletedTask;
    }

    public Guid? HoldingLockFor { get; private set; }

    public async Task<T> RunExclusivelyAsync<T>(
        Guid tenantId,
        Func<Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        HoldingLockFor = tenantId;

        try
        {
            return await work();
        }
        finally
        {
            HoldingLockFor = null;
        }
    }
}

public sealed class FakeTrustedDevices : ITrustedDevices
{
    public bool Trusted { get; set; }
    public bool TrustGranted { get; set; } = true;
    public List<string> TrustAttempts { get; } = [];
    public List<string> CheckedDeviceIds { get; } = [];
    public Task<bool> IsTrustedAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default)
    {
        CheckedDeviceIds.Add(deviceId);
        return Task.FromResult(Trusted);
    }

    public Exception? TrustThrows { get; set; }

    public Task<bool> TryTrustAsync(
        Guid userId,
        string deviceId,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        TrustAttempts.Add(deviceId);

        if (TrustThrows is not null)
        {
            throw TrustThrows;
        }

        return Task.FromResult(TrustGranted);
    }

    public List<(Guid UserId, Guid? DeviceId)> Revocations { get; } = [];
    public IReadOnlyList<TrustedDeviceInfo> Devices { get; set; } = [];

    public Task<IReadOnlyList<TrustedDeviceInfo>> GetDevicesAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Devices);

    public Task RevokeAsync(Guid userId, Guid? deviceId, CancellationToken cancellationToken = default)
    {
        Revocations.Add((userId, deviceId));
        return Task.CompletedTask;
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

    public List<(Guid UserId, string DeviceId, string Code)> Committed { get; } = [];

    public async Task<T> ConsumeAsync<T>(
        Guid userId,
        string deviceId,
        string code,
        Func<Task<T>> onConsumed,
        CancellationToken cancellationToken = default)
    {
        if (ConsumeThrows is not null)
        {
            throw ConsumeThrows;
        }

        Consumed.Add((userId, deviceId, code));

        var result = await onConsumed();

        Committed.Add((userId, deviceId, code));
        return result;
    }
}

public sealed class FakeJwtTokenService : IJwtTokenService
{
    public List<(Guid UserId, Guid TenantId, IReadOnlyCollection<string> Roles)> Requests { get; } = [];
    public Exception? Throws { get; set; }
    public AccessToken Create(Guid userId, Guid tenantId, IReadOnlyCollection<string> roles)
    {
        if (Throws is not null)
        {
            throw Throws;
        }

        Requests.Add((userId, tenantId, roles));
        return new AccessToken($"jwt-para-{userId}", 900);
    }
}

public sealed class FakeRefreshTokenService : IRefreshTokenService
{
    public const string IssuedToken = "refresh-fake-token";

    public List<(Guid TenantId, Guid UserId, string DeviceId)> Issued { get; } = [];

    public Task<string> IssueNewFamilyAsync(
        Guid tenantId,
        Guid userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        Issued.Add((tenantId, userId, deviceId));
        return Task.FromResult(IssuedToken);
    }

    public Task<RefreshTokenSession> RotateAsync(
        string presentedToken,
        string deviceId,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("el test de rotación vive en Stockma.Infrastructure.Tests");

    public Task RevokeFamilyAsync(string presentedToken, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
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

public sealed class FakeTenantSettingsProvider(ITenantContext tenantContext) : ITenantSettingsProvider
{
    public bool RequireSecondFactor { get; set; } = TenantSettings.DefaultRequireSecondFactor;
    public int Reads { get; private set; }
    public Guid? TenantIdWhenRead { get; private set; }
    public ExpiryThresholds Thresholds { get; set; } = ExpiryThresholds.Default();
    public TenantBranding? Branding { get; set; }

    public Task<ExpiryThresholds> GetExpiryThresholdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Thresholds);

    public Task<TenantBranding?> GetBrandingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Branding);

    public Task<bool> RequireSecondFactorAsync(CancellationToken cancellationToken = default)
    {
        Reads++;
        TenantIdWhenRead = tenantContext.TenantId;
        return Task.FromResult(RequireSecondFactor);
    }
}
