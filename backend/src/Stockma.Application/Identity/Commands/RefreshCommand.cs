using MediatR;
using Stockma.Application.Identity;
using Stockma.Domain.ValueObjects;

namespace Stockma.Application.Identity.Commands;

public sealed record RefreshCommand(string DeviceId) : IRequest<RefreshResult>
{
    public string? RefreshToken { get; set; }
}

public sealed record RefreshResult(string AccessToken, int ExpiresIn, string RefreshToken);

public sealed class RefreshCommandHandler(
    IRefreshTokenService sessions,
    IJwtTokenService tokens) : IRequestHandler<RefreshCommand, RefreshResult>
{
    public async Task<RefreshResult> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        var deviceId = DeviceIdentifier.Parse(command.DeviceId);

        var session = await sessions.RotateAsync(command.RefreshToken ?? string.Empty, deviceId, cancellationToken);

        var token = tokens.Create(session.UserId, session.TenantId, session.Roles);

        return new RefreshResult(token.Value, token.ExpiresInSeconds, session.PlainToken);
    }
}
