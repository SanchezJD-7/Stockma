using MediatR;
using Stockma.Application.Identity;

namespace Stockma.Application.Identity.Commands;

public sealed record LogoutCommand(string? RefreshToken) : IRequest;

public sealed class LogoutCommandHandler(IRefreshTokenService sessions)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand command, CancellationToken cancellationToken) =>
        await sessions.RevokeFamilyAsync(command.RefreshToken ?? string.Empty, cancellationToken);
}
