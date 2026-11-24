using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Waitlist;
using Stockma.Application.Waitlist.Commands;

namespace Stockma.Api.Controllers;

[ApiController]
[Route("api/waitlist")]
[AllowAnonymous]
public sealed class WaitlistController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WaitlistEntryDto>> Register(
        RegisterWaitlistEntryCommand command,
        CancellationToken cancellationToken)
    {
        var entry = await sender.Send(command, cancellationToken);
        return Created($"/api/waitlist/{entry.Id}", entry);
    }
}
