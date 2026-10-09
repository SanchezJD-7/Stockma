using System.IdentityModel.Tokens.Jwt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;

namespace Stockma.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class AdminUsersController(ISender sender, ILogger<AdminUsersController> logger) : ControllerBase
{
    [HttpPut("admin/users/{userId:guid}/phone-number")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    public async Task<ActionResult<SetUserPhoneNumberResult>> SetPhoneNumber(
        Guid userId,
        [FromBody] SetPhoneNumberRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = GetUserIdFromClaims();

        var result = await sender.Send(
            new SetUserPhoneNumberCommand(userId, request.PhoneNumber),
            cancellationToken);

        logger.LogInformation(
            "{AdminId} cambió el PhoneNumber del usuario {TargetUserId} a {PhoneNumberMasked}",
            adminId, userId, result.PhoneNumberMasked);

        return Ok(result);
    }

    private Guid GetUserIdFromClaims()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.Parse(sub ?? throw new UnauthorizedAccessException("El token no contiene el claim sub."));
    }

    public sealed record SetPhoneNumberRequest(string PhoneNumber);
}
