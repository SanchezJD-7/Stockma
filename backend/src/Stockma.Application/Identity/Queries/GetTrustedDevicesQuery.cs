using MediatR;

namespace Stockma.Application.Identity.Queries;

public sealed record GetTrustedDevicesQuery(Guid UserId) : IRequest<IReadOnlyList<TrustedDeviceInfo>>;

public sealed record TrustedDeviceInfo(
    Guid Id,
    Guid UserId,
    string DeviceId,
    DateTimeOffset TrustedAt,
    DateTimeOffset ExpiresAt,
    bool IsActive,
    DateTimeOffset? RevokedAt);

public sealed class GetTrustedDevicesQueryHandler : IRequestHandler<GetTrustedDevicesQuery, IReadOnlyList<TrustedDeviceInfo>>
{
    private readonly ITrustedDevices _devices;

    public GetTrustedDevicesQueryHandler(ITrustedDevices devices) => _devices = devices;

    public Task<IReadOnlyList<TrustedDeviceInfo>> Handle(GetTrustedDevicesQuery request, CancellationToken cancellationToken)
        => _devices.GetDevicesAsync(request.UserId, cancellationToken);
}
