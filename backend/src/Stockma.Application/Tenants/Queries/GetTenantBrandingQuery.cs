using MediatR;

namespace Stockma.Application.Tenants.Queries;

public sealed record GetTenantBrandingQuery : IRequest<TenantBrandingDto?>;

public sealed class GetTenantBrandingQueryHandler(ITenantSettingsProvider tenantSettings)
    : IRequestHandler<GetTenantBrandingQuery, TenantBrandingDto?>
{
    public async Task<TenantBrandingDto?> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        var branding = await tenantSettings.GetBrandingAsync(cancellationToken);
        return branding is null
            ? null
            : new TenantBrandingDto(branding.Primary, branding.PrimaryActive, branding.PrimaryBg);
    }
}
