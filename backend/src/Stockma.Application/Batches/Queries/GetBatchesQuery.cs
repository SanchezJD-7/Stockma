using MediatR;
using Stockma.Domain.Services;

namespace Stockma.Application.Batches.Queries;

public sealed record GetBatchesQuery(Guid ProductId) : IRequest<IReadOnlyList<BatchDto>>;
public sealed class GetBatchesQueryHandler(
    IBatchRepository batches,
    ITenantSettingsProvider tenantSettings,
    TimeProvider timeProvider) : IRequestHandler<GetBatchesQuery, IReadOnlyList<BatchDto>>
{
    public async Task<IReadOnlyList<BatchDto>> Handle(
        GetBatchesQuery query,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var thresholds = await tenantSettings.GetExpiryThresholdsAsync(cancellationToken);

        var found = await batches.GetByProductAsync(query.ProductId, cancellationToken);

        return
        [
            .. found.Select(batch => BatchDto.From(
                batch,
                BatchStatusCalculator.Evaluate(batch.ExpirationDate, today, thresholds)))
        ];
    }
}
