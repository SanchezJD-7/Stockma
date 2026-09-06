using MediatR;
using Stockma.Application.Common;
using Stockma.Application.Products;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Batches.Commands;

public sealed record RegisterBatchCommand(
    Guid ProductId,
    string LotNumber,
    DateOnly ExpirationDate,
    int InitialQuantity,
    string LocationShelf) : IRequest<BatchDto>;

public sealed class RegisterBatchCommandHandler(
    IBatchRepository batches,
    IProductRepository products,
    ITenantContext tenantContext) : IRequestHandler<RegisterBatchCommand, BatchDto>
{
    public async Task<BatchDto> Handle(
        RegisterBatchCommand command,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(command.ProductId, cancellationToken)
            ?? throw new ProductNotFoundForBatchException(command.ProductId);

        var batch = new Batch(
            tenantId: tenantContext.TenantId,
            productId: product.Id,
            lotNumber: command.LotNumber,
            expirationDate: command.ExpirationDate,
            initialQuantity: command.InitialQuantity,
            locationShelf: command.LocationShelf);

        await batches.AddAsync(batch, cancellationToken);
        return BatchDto.From(batch);
    }
}
