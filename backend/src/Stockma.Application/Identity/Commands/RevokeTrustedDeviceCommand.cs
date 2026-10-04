using MediatR;

namespace Stockma.Application.Identity.Commands;

public sealed record RevokeTrustedDeviceCommand(Guid UserId, Guid? DeviceId) : IRequest;

public sealed class RevokeTrustedDeviceCommandHandler : IRequestHandler<RevokeTrustedDeviceCommand>
{
    private readonly ITrustedDevices _devices;

    public RevokeTrustedDeviceCommandHandler(ITrustedDevices devices) => _devices = devices;

    public async Task Handle(RevokeTrustedDeviceCommand command, CancellationToken cancellationToken)
        => await _devices.RevokeAsync(command.UserId, command.DeviceId, cancellationToken);
}
