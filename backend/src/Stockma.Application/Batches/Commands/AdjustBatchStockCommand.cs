using MediatR;

namespace Stockma.Application.Batches.Commands;

public sealed record AdjustBatchStockCommand(Guid BatchId, int Delta) : IRequest<BatchDto>;

public sealed class AdjustBatchStockCommandHandler(IBatchRepository batches) : IRequestHandler<AdjustBatchStockCommand, BatchDto>
{
    public async Task<BatchDto> Handle(
        AdjustBatchStockCommand command,
        CancellationToken cancellationToken)
    {
        var batch = await batches.AdjustAsync(command.BatchId, command.Delta, cancellationToken);

        return BatchDto.From(batch);
    }
}
