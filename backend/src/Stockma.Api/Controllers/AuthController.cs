using System.IdentityModel.Tokens.Jwt;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;

namespace Stockma.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    [Authorize(Policy = AuthorizationPolicies.TenantAdmin)]
    public async Task<ActionResult<RegisteredUser>> Register(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var user = await sender.Send(command, cancellationToken);
        return Created($"/api/users/{user.UserId}", user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResult>> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        if (result.RefreshToken is not null)
        {
            SetRefreshCookie(result.RefreshToken);
        }

        return Ok(result);
    }

    [HttpPost("confirm-device")]
    [AllowAnonymous]
    public async Task<ActionResult<ConfirmDeviceResult>> ConfirmDevice(ConfirmDeviceCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        SetRefreshCookie(result.RefreshToken);

        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<RefreshResult>> Refresh(RefreshCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            command with { RefreshToken = Request.Cookies[RefreshCookie.Name] },
            cancellationToken);

        SetRefreshCookie(result.RefreshToken);

        return Ok(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sender.Send(new LogoutCommand(Request.Cookies[RefreshCookie.Name]), cancellationToken);

        ClearRefreshCookie();

        return NoContent();
    }

    [HttpPut("phone-number")]
    [Authorize]
    public async Task<ActionResult<RequestPhoneNumberChangeResult>> RequestPhoneNumberChange(
        RequestPhoneNumberChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestPhoneNumberChangeCommand(GetUserIdFromClaims(), request.NewPhoneNumber),
            cancellationToken);

        return Accepted(result);
    }

    [HttpPost("phone-number/confirm")]
    [Authorize]
    public async Task<ActionResult<ConfirmPhoneNumberChangeResult>> ConfirmPhoneNumberChange(
        ConfirmPhoneNumberChangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfirmPhoneNumberChangeCommand(GetUserIdFromClaims(), request.Otp),
            cancellationToken);

        return Ok(result);
    }

    public sealed record RequestPhoneNumberChangeRequest(string NewPhoneNumber);
    public sealed record ConfirmPhoneNumberChangeRequest(string Otp);

    private Guid GetUserIdFromClaims()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(sub, out var userId) ? userId : throw new UnauthorizedAccessException("El token no trae un sub válido.");
    }

    private void SetRefreshCookie(string token) =>
        Response.Headers.Append("Set-Cookie", RefreshCookie.BuildSetCookieValue(token));

    private void ClearRefreshCookie() =>
        Response.Headers.Append("Set-Cookie", RefreshCookie.BuildClearCookieValue());
}
