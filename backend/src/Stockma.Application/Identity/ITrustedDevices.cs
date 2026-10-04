using Stockma.Application.Identity.Queries;

namespace Stockma.Application.Identity;

public interface ITrustedDevices
{
    Task<bool> IsTrustedAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);
    Task<bool> TryTrustAsync(Guid userId, string deviceId, string fingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrustedDeviceInfo>> GetDevicesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid userId, Guid? deviceId, CancellationToken cancellationToken = default);
}
