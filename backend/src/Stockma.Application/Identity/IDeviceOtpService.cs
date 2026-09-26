namespace Stockma.Application.Identity;

public interface IDeviceOtpService
{
    Task IssueAsync(Guid userId, string deviceId, CancellationToken cancellationToken = default);

    Task ConsumeAsync(Guid userId, string deviceId, string code, CancellationToken cancellationToken = default);
}
