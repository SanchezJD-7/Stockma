using System.IdentityModel.Tokens.Jwt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Application.Identity.Queries;

namespace Stockma.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class DevicesController(ISender sender) : ControllerBase
{
    [HttpGet("auth/devices")]
    public async Task<ActionResult<IReadOnlyList<TrustedDeviceInfo>>> GetMyDevices(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var devices = await sender.Send(new GetTrustedDevicesQuery(userId), cancellationToken);
        return Ok(devices);
    }

    [HttpPost("auth/devices/{deviceId:guid}/revoke")]
    public async Task<IActionResult> RevokeMyDevice(Guid deviceId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        await sender.Send(new RevokeTrustedDeviceCommand(userId, deviceId), cancellationToken);
        return NoContent();
    }

    private Guid GetUserIdFromClaims()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.Parse(sub ?? throw new UnauthorizedAccessException("El token no contiene el claim sub."));
    }

    [HttpGet("admin/users/{userId:guid}/devices")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    public async Task<ActionResult<IReadOnlyList<TrustedDeviceInfo>>> GetUserDevices(Guid userId, CancellationToken cancellationToken)
    {
        var devices = await sender.Send(new GetTrustedDevicesQuery(userId), cancellationToken);
        return Ok(devices);
    }

    [HttpPost("admin/users/{userId:guid}/devices/{deviceId:guid}/revoke")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    public async Task<IActionResult> RevokeUserDevice(Guid userId, Guid deviceId, CancellationToken cancellationToken)
    {
        await sender.Send(new RevokeTrustedDeviceCommand(userId, deviceId), cancellationToken);
        return NoContent();
    }

    [HttpPost("admin/users/{userId:guid}/devices/revoke-all")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    public async Task<IActionResult> RevokeAllUserDevices(Guid userId, CancellationToken cancellationToken)
    {
        await sender.Send(new RevokeTrustedDeviceCommand(userId, null), cancellationToken);
        return NoContent();
    }
}
