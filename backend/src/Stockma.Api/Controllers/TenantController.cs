using MediatR;
using Microsoft.AspNetCore.Mvc;
using Stockma.Application.Tenants;
using Stockma.Application.Tenants.Queries;

namespace Stockma.Api.Controllers;

[ApiController]
[Route("api/tenant")]
public sealed class TenantController(ISender sender) : ControllerBase
{
    [HttpGet("branding")]
    public async Task<ActionResult<TenantBrandingDto?>> GetBranding(CancellationToken cancellationToken)
    {
        var branding = await sender.Send(new GetTenantBrandingQuery(), cancellationToken);
        return new JsonResult(branding);
    }
}
